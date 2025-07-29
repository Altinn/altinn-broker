using Altinn.ApiClients.Maskinporten.Config;
using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Integrations.Altinn.Authorization;
using Altinn.Broker.Integrations.Altinn.Events;
using Altinn.Broker.Integrations.Altinn.Register;
using Altinn.Broker.Integrations.Altinn.ResourceRegistry;
using Altinn.Broker.Integrations.Azure;
using Altinn.Broker.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    }
}
