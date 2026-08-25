using Altinn.Broker.API.AltinnPlatformAuth.Options;
using Altinn.Broker.Core.Options;

using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Altinn.Broker.API.AltinnPlatformAuth;

public static class DependencyInjection
{
    public static IServiceCollection AddAltinnPlatformAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AltinnPlatformAuthSettings>(configuration.GetSection(AltinnPlatformAuthSettings.SectionName));
        services.AddHttpClient<IAltinnPlatformAuthenticationClient, AltinnPlatformAuthenticationClient>();
        services.AddSingleton<IAltinnPlatformJwtCookieReader, AltinnPlatformJwtCookieReader>();
        return services;
    }
}
