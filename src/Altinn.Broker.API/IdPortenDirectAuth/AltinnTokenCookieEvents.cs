using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Tus;
using Altinn.Broker.Integrations.Altinn;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Altinn.Broker.API.IdPortenDirectAuth;

/// <summary>
/// On each request authenticated via cookie, validates the stored Altinn token.
/// If it is expired or close to expiring, refreshes the ID-Porten session and re-exchanges it.
/// Sets ClaimsPrincipal from the Altinn token so downstream authorization sees urn:altinn:* claims.
/// Rejects sessions revoked via ID-Porten back-channel logout.
/// For in-progress TUS uploads, accepts an expired Altinn cookie token the same way expired
/// Maskinporten bearer tokens are accepted — so overnight browser uploads can finish.
/// </summary>
public class AltinnTokenCookieEvents : CookieAuthenticationEvents
{
    /// <summary>
    /// Re-exchange this far ahead of expiry, so no request travels downstream with a token that
    /// expires mid-flight. Keep it small: Altinn issues two-minute tokens, so a larger leeway
    /// renews on every request.
    /// </summary>
    private static readonly TimeSpan ExpiryLeeway = TimeSpan.FromSeconds(20);

    private readonly IOidcBackChannelLogoutSessionStore _logoutSessionStore;
    private readonly IIdPortenTokenRefreshService _tokenRefreshService;
    private readonly ITusUploadSessionAuthenticationHelper _tusUploadSessionAuthenticationHelper;

    public AltinnTokenCookieEvents(
        IOidcBackChannelLogoutSessionStore logoutSessionStore,
        IIdPortenTokenRefreshService tokenRefreshService,
        ITusUploadSessionAuthenticationHelper tusUploadSessionAuthenticationHelper)
    {
        _logoutSessionStore = logoutSessionStore;
        _tokenRefreshService = tokenRefreshService;
        _tusUploadSessionAuthenticationHelper = tusUploadSessionAuthenticationHelper;
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

        // Fully expired: prefer TUS active-upload grace before calling ID-Porten on every chunk.
        // Overnight uploads outlive authorization_lifetime; hammering refresh with invalid_grant
        // would add latency and noise for no benefit once the upload session is established.
        if (jwt!.ValidTo <= DateTime.UtcNow)
        {
            if (await TryAcceptActiveTusUploadAsync(context, altinnToken, sid))
            {
                return;
            }
        }

        if (jwt.ValidTo - ExpiryLeeway <= DateTime.UtcNow)
        {
            var refreshed = await TryReExchange(context, tokens);
            if (!refreshed)
            {
                if (await TryAcceptActiveTusUploadAsync(context, altinnToken, sid))
                {
                    return;
                }

                // Reject this request only. Do not SignOut — a parallel TUS chunk may already have
                // rotated the refresh token and written a newer cookie; deleting ours races that.
                context.RejectPrincipal();
                return;
            }

            var renewedAltinnToken = context.Properties.GetTokenValue(OidcSessionKeys.AltinnToken);
            if (string.IsNullOrEmpty(renewedAltinnToken) || !CanRead(renewedAltinnToken, out jwt))
            {
                // Prefer the pre-refresh token for TUS grace; the exchange may have failed after
                // ID-Porten rotated credentials without storing a usable Altinn JWT.
                if (await TryAcceptActiveTusUploadAsync(context, altinnToken, sid))
                {
                    return;
                }

                context.RejectPrincipal();
                return;
            }

            altinnToken = renewedAltinnToken;
        }

        ApplyPrincipal(context, jwt!, sid);
    }

    /// <summary>
    /// Before any Set-Cookie, adopt a refresh rotation published by a parallel request. A long
    /// TUS PATCH that authenticated with a spent refresh token must not overwrite the newer cookie.
    /// Follows successive cache hops when several rotations happened while the request was in flight.
    /// </summary>
    public override async Task SigningIn(CookieSigningInContext context)
    {
        var refreshToken = context.Properties.GetTokenValue(OidcSessionKeys.IdPortenRefreshToken);
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var rotated = await ResolveLatestCachedRotationAsync(refreshToken);
        if (rotated is null)
        {
            return;
        }

        var tokenExchange = context.HttpContext.RequestServices.GetRequiredService<IAltinnTokenExchangeService>();
        string? newAltinnToken;
        try
        {
            newAltinnToken = await tokenExchange.ExchangeIdPortenToken(rotated.AccessToken, CancellationToken.None);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Keep the rotated refresh token so the next request does not rewrite a spent one.
            RetainRotatedRefreshToken(context, rotated);
            return;
        }

        if (string.IsNullOrEmpty(newAltinnToken))
        {
            RetainRotatedRefreshToken(context, rotated);
            return;
        }

        context.Properties.StoreTokens(
        [
            new AuthenticationToken { Name = OidcSessionKeys.AltinnToken, Value = newAltinnToken },
            new AuthenticationToken { Name = OidcSessionKeys.IdPortenRefreshToken, Value = rotated.RefreshToken }
        ]);
    }

    /// <summary>
    /// When Altinn exchange fails after a cached rotation was found, still persist the new refresh
    /// token and keep the existing Altinn JWT. Leaving the spent refresh token would let this
    /// SignIn overwrite a newer cookie written by a parallel request.
    /// </summary>
    private static void RetainRotatedRefreshToken(CookieSigningInContext context, IdPortenTokens rotated)
    {
        var currentAltinn = context.Properties.GetTokenValue(OidcSessionKeys.AltinnToken);
        if (string.IsNullOrEmpty(currentAltinn))
        {
            context.Properties.StoreTokens(
            [
                new AuthenticationToken { Name = OidcSessionKeys.IdPortenRefreshToken, Value = rotated.RefreshToken }
            ]);
            return;
        }

        context.Properties.StoreTokens(
        [
            new AuthenticationToken { Name = OidcSessionKeys.AltinnToken, Value = currentAltinn },
            new AuthenticationToken { Name = OidcSessionKeys.IdPortenRefreshToken, Value = rotated.RefreshToken }
        ]);
    }

    private const int MaxCachedRotationHops = 5;

    private async Task<IdPortenTokens?> ResolveLatestCachedRotationAsync(string refreshToken)
    {
        var current = refreshToken;
        IdPortenTokens? latest = null;

        for (var hop = 0; hop < MaxCachedRotationHops; hop++)
        {
            var rotated = await _tokenRefreshService.GetCachedRotationAsync(current, CancellationToken.None);
            if (rotated is null || rotated.RefreshToken == current)
            {
                break;
            }

            latest = rotated;
            current = rotated.RefreshToken;
        }

        return latest;
    }

    private async Task<bool> TryAcceptActiveTusUploadAsync(
        CookieValidatePrincipalContext context,
        string? altinnToken,
        string? sid)
    {
        if (string.IsNullOrEmpty(altinnToken))
        {
            return false;
        }

        var principal = await _tusUploadSessionAuthenticationHelper.TryAcceptExpiredAltinnCookieForActiveUploadAsync(
            context.HttpContext,
            altinnToken,
            sid,
            context.Principal,
            CancellationToken.None);
        if (principal is null)
        {
            return false;
        }

        context.ReplacePrincipal(principal);
        // Keep the session cookie sliding so overnight TUS traffic does not hit cookie expiry
        // even though ID-Porten can no longer renew the embedded Altinn token.
        context.ShouldRenew = true;
        return true;
    }

    private static void ApplyPrincipal(CookieValidatePrincipalContext context, JwtSecurityToken jwt, string? sid)
    {
        var identity = new ClaimsIdentity(
            jwt.Claims,
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

        // ShouldRenew is left as the middleware set it; clearing it would discard a refreshed
        // token and disable sliding expiration.
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
    /// Trades the stored refresh token for a fresh ID-Porten access token and exchanges that for a
    /// new Altinn token. Without it the session dies at the Altinn token's expiry even though the
    /// cookie is still valid.
    /// </summary>
    private async Task<bool> TryReExchange(CookieValidatePrincipalContext context, List<AuthenticationToken> tokens)
    {
        var refreshToken = tokens.FirstOrDefault(t => t.Name == OidcSessionKeys.IdPortenRefreshToken)?.Value;
        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        // Not tied to RequestAborted: a cancelled refresh would leave the rotated token unrecorded.
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
