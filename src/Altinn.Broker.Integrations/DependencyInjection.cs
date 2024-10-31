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
using Altinn.Correspondence.Integrations.Slack;
using Slack.Webhooks;

namespace Altinn.Broker.Integrations;
public static class DependencyInjection
{
    public static void AddIntegrations(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        services.AddSingleton<IResourceManager, AzureResourceManagerService>();
        services.AddSingleton<IBrokerStorageService, BlobService>();
        services.AddScoped<IAltinnResourceRepository, AltinnResourceRegistryRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<IAuthorizationService, AltinnAuthorizationService>();
        services.AddScoped<IIdempotencyEventRepository, IdempotencyEventRepository>();
        services.AddScoped<IEventBus, AltinnEventBus>();
        services.AddScoped<IAltinnRegisterService, AltinnRegisterService>();

        var maskinportenSettings = new MaskinportenSettings();
        configuration.GetSection(nameof(MaskinportenSettings)).Bind(maskinportenSettings);
        var altinnOptions = new AltinnOptions();
        configuration.GetSection(nameof(AltinnOptions)).Bind(altinnOptions);

        if (false)
        {
            services.AddSingleton<IEventBus, ConsoleLogEventBus>();
        }
        else
        {
            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IEventBus).FullName, maskinportenSettings);
            services.AddHttpClient<IEventBus, AltinnEventBus>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IEventBus>();

            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IAltinnRegisterService).FullName, maskinportenSettings);
            services.AddHttpClient<IAltinnRegisterService, AltinnRegisterService>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IAltinnRegisterService>();

            services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(typeof(IAuthorizationService).FullName, maskinportenSettings);
            services.AddHttpClient<IAuthorizationService, AltinnAuthorizationService>((client) => client.BaseAddress = new Uri(altinnOptions.PlatformGatewayUrl))
                    .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition, IAuthorizationService>();
        }
        var generalSettings = new GeneralSettings();
        configuration.GetSection(nameof(GeneralSettings)).Bind(generalSettings);
        if (string.IsNullOrWhiteSpace(generalSettings.SlackUrl))
        {
            services.AddSingleton<ISlackClient>(new SlackDevClient(""));
        } 
        else
        {
            services.AddSingleton<ISlackClient>(new SlackClient(generalSettings.SlackUrl));
        }
    }
}
