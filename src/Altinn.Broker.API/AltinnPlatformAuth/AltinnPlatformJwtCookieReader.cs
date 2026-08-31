using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Altinn.Broker.API.AltinnPlatformAuth.Options;
using Altinn.Broker.API.Configuration;
using Altinn.Broker.Core.Options;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Broker.API.AltinnPlatformAuth;

public interface IAltinnPlatformJwtCookieReader
{
    Task<ClaimsPrincipal?> ReadAuthenticatedPrincipalAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates the Altinn platform JWT stored in the runtime httpOnly cookie (after refresh).
/// </summary>
public sealed class AltinnPlatformJwtCookieReader : IAltinnPlatformJwtCookieReader
{
    private readonly AltinnPlatformAuthSettings _platformAuthSettings;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

    public AltinnPlatformJwtCookieReader(
        IOptions<AltinnPlatformAuthSettings> platformAuthSettings,
        IOptions<AltinnOptions> altinnOptions)
    {
        _platformAuthSettings = platformAuthSettings.Value;
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            altinnOptions.Value.OpenIdWellKnown,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });
    }

    public async Task<ClaimsPrincipal?> ReadAuthenticatedPrincipalAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_platformAuthSettings.JwtCookieName)
            || !httpContext.Request.Cookies.TryGetValue(_platformAuthSettings.JwtCookieName, out var token)
            || string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);
            // Altinn platform end-user JWTs are not issued with a Broker-specific audience.
            // No ValidAudience is configured on AltinnPlatformAuthSettings. ValidateAudience stays
            // disabled (same as JwtBearerDefaults for Altinn tokens). This reader is used by the
            // AltinnPlatformJwtCookie auth scheme and as a fallback for AuthenticationController.Me.
            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = false,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidIssuer = configuration.Issuer,
                IssuerSigningKeys = configuration.SigningKeys
            };

            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, parameters, out _);
            var identity = new ClaimsIdentity(
                principal.Claims,
                AuthorizationConstants.AltinnPlatformJwtCookie,
                ClaimTypes.Name,
                ClaimTypes.Role);
            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return null;
        }
    }
}
