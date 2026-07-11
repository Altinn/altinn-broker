using Altinn.ApiClients.Maskinporten.Config;
using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Broker.Application.UploadFile;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Integrations.Altinn.Authorization;
using Altinn.Broker.Integrations.Altinn.Events;
using Altinn.Broker.Integrations.Altinn.Register;
using Altinn.Broker.Integrations.Altinn.ResourceRegistry;
using Altinn.Broker.Integrations.Azure;
using Altinn.Broker.Integrations.Maskinporten;
using Altinn.Broker.Persistence.Repositories;
using Altinn.Broker.Integrations.Tus;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

using StackExchange.Redis;

using Xtensible.TusDotNet.Azure;
using Altinn.Broker.Integrations.Slack;
using Slack.Webhooks;
using Altinn.Broker.Core.Helpers;

namespace Altinn.Broker.Integrations;
public static class DependencyInjection
{
    public static void AddIntegrations(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        services.AddSingleton<IResourceManager, AzureResourceManagerService>();
        services.AddSingleton<IBrokerStorageService, AzureStorageService>();
        services.AddScoped<IAltinnResourceRepository, AltinnResourceRegistryRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddSingleton<IIdempotencyEventRepository, IdempotencyEventRepository>();
        services.AddSingleton<IKeyVaultSecretStore, KeyVaultSecretStore>();
        services.AddSingleton<IContainerAppRefreshService, AzureContainerAppRefreshService>();
        services.AddSingleton<IMaskinportenJwkGenerator, MaskinportenJwkGenerator>();
        services.AddScoped<IMaskinportenTokenService, MaskinportenTokenService>();
        services.AddScoped<IDigdirMaskinportenAdminService, DigdirMaskinportenAdminService>();
        services.AddScoped<IMaskinportenJwkRotationService, MaskinportenJwkRotationService>();

        var maskinportenSettings = new MaskinportenSettings();
        configuration.GetSection(nameof(MaskinportenSettings)).Bind(maskinportenSettings);
        var altinnOptions = new AltinnOptions();
        configuration.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);

        if (string.IsNullOrWhiteSpace(maskinportenSettings.ClientId))
        {
            services.AddSingleton<IEventBus, ConsoleLogEventBus>();
            services.AddScoped<IAuthorizationService, AltinnAuthorizationService>();
        }
        else
        {
            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IEventBus).FullName, maskinportenSettings);
            services.AddHttpClient<IEventBus, AltinnEventBus>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IEventBus>()
                .AddStandardRetryPolicy();

            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IAltinnRegisterService).FullName, maskinportenSettings);
            services.AddHttpClient<IAltinnRegisterService, AltinnRegisterService>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IAltinnRegisterService>()
                .AddStandardRetryPolicy();

            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IAuthorizationService).FullName, maskinportenSettings);
            services.AddHttpClient<IAuthorizationService, AltinnAuthorizationService>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                    .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IAuthorizationService>()
                    .AddStandardRetryPolicy();
            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IAltinnResourceRepository).FullName, maskinportenSettings);
            services.AddHttpClient<IAltinnResourceRepository, AltinnResourceRegistryRepository>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                    .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IAltinnResourceRepository>()
                    .AddStandardRetryPolicy();
        }
        var generalSettings = new GeneralSettings();
        configuration.GetSection(nameof(GeneralSettings)).Bind(generalSettings);
        if (string.IsNullOrWhiteSpace(generalSettings.SlackUrl))
        {
            services.AddSingleton<ISlackClient>(new SlackDevClient(""));
        } 
        else
        {
            services.AddHttpClient(nameof(SlackClient))
                .AddStandardRetryPolicy();
            services.AddSingleton<ISlackClient>(serviceProvider =>
            {
                var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(SlackClient));
                return new SlackClient(generalSettings.SlackUrl, httpClient: httpClient);
            });
        }

        services.AddSingleton<SlackSettings>();
        services.AddSingleton<SlackExceptionNotificationHandler>();
        services.AddExceptionHandler<SlackExceptionNotificationHandler>();

        AddTusUploads(services, configuration);
    }

    private static void AddTusUploads(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.Configure<TusOptions>(configuration.GetSection(TusOptions.SectionName));
        services.AddSingleton<ITusExpirationDetailsStore>(serviceProvider =>
        {
            var multiplexer = serviceProvider.GetService<IConnectionMultiplexer>();
            return multiplexer is not null
                ? new RedisTusExpirationDetailsStore(multiplexer)
                : new NullExpirationDetailsStore();
        });
        services.AddSingleton<ITusPartialUploadRegistry>(serviceProvider =>
            new TusPartialUploadRegistry(
                serviceProvider.GetRequiredService<IDistributedCache>(),
                serviceProvider.GetService<IConnectionMultiplexer>()));
        services.AddSingleton<ITusConcatJobCoordinator, TusConcatJobCoordinator>();
        services.AddSingleton<ITusUploadStateRegistry, TusUploadStateRegistry>();
        services.AddSingleton<ITusUploadProgressCache>(serviceProvider =>
            new TusUploadProgressCache(
                serviceProvider.GetRequiredService<HybridCache>(),
                serviceProvider.GetRequiredService<ILogger<TusUploadProgressCache>>(),
                serviceProvider.GetService<IConnectionMultiplexer>()));
        services.AddSingleton<ITusUploadActivityCache, TusUploadActivityCache>();
        services.AddScoped<BrokerTusStore>();
        services.AddScoped<ITusStorageResolver, TusStorageResolver>();
        services.AddScoped<ITusUploadFinalizationService, TusUploadFinalizationService>();
        services.AddScoped<ITusUploadFinalizationProgressService, TusUploadFinalizationProgressService>();
        services.AddSingleton<ITusUploadKindResolver, TusUploadKindResolver>();
    }
}
