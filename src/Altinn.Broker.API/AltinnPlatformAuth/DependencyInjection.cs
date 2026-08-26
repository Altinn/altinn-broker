using Altinn.Broker.API.AltinnPlatformAuth.Options;
using Altinn.Broker.API.Configuration;

using Microsoft.AspNetCore.Authentication;

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

    public static AuthenticationBuilder AddAltinnPlatformJwtCookie(
        this AuthenticationBuilder builder)
    {
        return builder.AddScheme<AuthenticationSchemeOptions, AltinnPlatformJwtCookieAuthenticationHandler>(
            AuthorizationConstants.AltinnPlatformJwtCookie,
            _ => { });
    }
}
