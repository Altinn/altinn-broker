using Altinn.Broker.API.AltinnPlatformAuth.Options;
using Altinn.Broker.API.Configuration;
using Altinn.Broker.Core.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.API.Authentication;

/// <summary>
/// Reads the end user's Altinn token from whichever session the request authenticated with:
/// the ID-Porten direct session cookie, or the shared Altinn platform runtime JWT cookie.
/// </summary>
public sealed class EndUserTokenProvider : IEndUserTokenProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AltinnPlatformAuthSettings _platformAuthSettings;

    public EndUserTokenProvider(IHttpContextAccessor httpContextAccessor, IOptions<AltinnPlatformAuthSettings> platformAuthSettings)
    {
        _httpContextAccessor = httpContextAccessor;
        _platformAuthSettings = platformAuthSettings.Value;
    }

    public async Task<string?> GetAltinnToken()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        // IdPortenDirectAuth stores the exchanged Altinn token in the session cookie properties.
        var sessionToken = await httpContext.GetTokenAsync(AuthorizationConstants.EndUserCookie, "altinn_token");
        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            return sessionToken;
        }

        // AltinnPlatformAuth keeps the Altinn token in the runtime cookie itself.
        if (!string.IsNullOrWhiteSpace(_platformAuthSettings.JwtCookieName)
            && httpContext.Request.Cookies.TryGetValue(_platformAuthSettings.JwtCookieName, out var platformToken)
            && !string.IsNullOrWhiteSpace(platformToken))
        {
            return platformToken;
        }

        return null;
    }
}
