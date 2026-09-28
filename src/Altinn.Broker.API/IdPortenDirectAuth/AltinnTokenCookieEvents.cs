using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Integrations.Altinn;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Altinn.Broker.API.IdPortenDirectAuth;

/// <summary>
/// On each request authenticated via cookie, validates the stored Altinn token.
/// If it is expired or close to expiring, refreshes the ID-Porten session and re-exchanges it.
/// Sets ClaimsPrincipal from the Altinn token so downstream authorization sees urn:altinn:* claims.
/// Rejects sessions revoked via ID-Porten back-channel logout.
/// </summary>
public class AltinnTokenCookieEvents : CookieAuthenticationEvents
{
    /// <summary>
    /// Re-exchange this far ahead of the Altinn token's expiry, so no request travels downstream
    /// with a token that expires mid-flight.
    /// </summary>
    private static readonly TimeSpan ExpiryLeeway = TimeSpan.FromMinutes(2);

    private readonly IOidcBackChannelLogoutSessionStore _logoutSessionStore;
    private readonly IIdPortenTokenRefreshService _tokenRefreshService;

    public AltinnTokenCookieEvents(
        IOidcBackChannelLogoutSessionStore logoutSessionStore,
        IIdPortenTokenRefreshService tokenRefreshService)
    {
        _logoutSessionStore = logoutSessionStore;
        _tokenRefreshService = tokenRefreshService;
    }

    /// <summary>
    /// Cookie challenges must not 302 to a login page (that nests returnUrl forever for APIs).
    /// The SPA initiates login via GET /broker/api/v1/authentication/login (OIDC Challenge).
    /// </summary>
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var sid = GetItem(context.Properties, OidcSessionKeys.Sid);
        var sub = GetItem(context.Properties, OidcSessionKeys.Sub);
        if (await _logoutSessionStore.IsRevokedAsync(sid, sub, context.Properties.IssuedUtc))
        {
            await EndSession(context);
            return;
        }

        var tokens = context.Properties.GetTokens().ToList();
        var altinnToken = tokens.FirstOrDefault(t => t.Name == OidcSessionKeys.AltinnToken)?.Value;

        if (string.IsNullOrEmpty(altinnToken) || !CanRead(altinnToken, out var jwt))
        {
            await EndSession(context);
            return;
        }

        if (jwt!.ValidTo - ExpiryLeeway <= DateTime.UtcNow)
        {
            var refreshed = await TryReExchange(context, tokens);
            if (!refreshed)
            {
                await EndSession(context);
                return;
            }

            altinnToken = context.Properties.GetTokenValue(OidcSessionKeys.AltinnToken);
            if (string.IsNullOrEmpty(altinnToken) || !CanRead(altinnToken, out jwt))
            {
                await EndSession(context);
                return;
            }
        }

        var identity = new ClaimsIdentity(
            jwt!.Claims,
            AuthorizationConstants.EndUserCookie,
            ClaimTypes.Name,
            ClaimTypes.Role);
        if (!string.IsNullOrEmpty(sid) && !identity.HasClaim("sid", sid))
        {
            identity.AddClaim(new Claim("sid", sid));
        }

        var idPortenIdentity = IdPortenPrincipalClaims.CopyIdentity(context.Principal);
        context.ReplacePrincipal(idPortenIdentity is null
            ? new ClaimsPrincipal(identity)
            : new ClaimsPrincipal([identity, idPortenIdentity]));

        // ShouldRenew is left as the cookie middleware set it: forcing it to false here would both
        // discard a token refreshed above and disable the configured sliding expiration.
    }

    private static async Task EndSession(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(AuthorizationConstants.EndUserCookie);
    }

    private static bool CanRead(string token, out JwtSecurityToken? jwt)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            jwt = null;
            return false;
        }

        jwt = handler.ReadJwtToken(token);
        return true;
    }

    /// <summary>
    /// Trades the stored ID-Porten refresh token for a fresh access token and exchanges that for a
    /// new Altinn token. Without this the session dies at the Altinn token's expiry even though the
    /// cookie and the ID-Porten session are both still alive, and the user is bounced through login.
    /// </summary>
    private async Task<bool> TryReExchange(CookieValidatePrincipalContext context, List<AuthenticationToken> tokens)
    {
        var refreshToken = tokens.FirstOrDefault(t => t.Name == OidcSessionKeys.IdPortenRefreshToken)?.Value;
        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        // Not tied to RequestAborted: a cancelled refresh would leave ID-Porten's rotated token
        // unrecorded, and the next request would find a refresh token that is already spent.
        var refreshed = await _tokenRefreshService.RefreshAsync(refreshToken, CancellationToken.None);
        if (refreshed is null)
        {
            return false;
        }

        var tokenExchange = context.HttpContext.RequestServices.GetRequiredService<IAltinnTokenExchangeService>();
        var newAltinnToken = await tokenExchange.ExchangeIdPortenToken(refreshed.AccessToken, CancellationToken.None);
        if (string.IsNullOrEmpty(newAltinnToken))
        {
            return false;
        }

        context.Properties.StoreTokens(
        [
            new AuthenticationToken { Name = OidcSessionKeys.AltinnToken, Value = newAltinnToken },
            new AuthenticationToken { Name = OidcSessionKeys.IdPortenRefreshToken, Value = refreshed.RefreshToken }
        ]);
        context.ShouldRenew = true;
        return true;
    }

    private static string? GetItem(AuthenticationProperties properties, string key)
        => properties.Items.TryGetValue(key, out var value) ? value : null;
}
