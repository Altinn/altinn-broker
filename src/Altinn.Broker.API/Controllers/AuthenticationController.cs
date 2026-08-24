using Altinn.Broker.API.Authentication;
using Altinn.Broker.API.Configuration;
using Altinn.Broker.Core.Options;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.API.Controllers;

[ApiController]
[Route("broker/api/v1/authentication")]
public class AuthenticationController : ControllerBase
{
    private readonly IdPortenSettings _idPortenSettings;
    private readonly IOidcLogoutTokenValidator _logoutTokenValidator;
    private readonly IOidcBackChannelLogoutSessionStore _logoutSessionStore;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(
        IOptions<IdPortenSettings> idPortenSettings,
        IOidcLogoutTokenValidator logoutTokenValidator,
        IOidcBackChannelLogoutSessionStore logoutSessionStore,
        ILogger<AuthenticationController> logger)
    {
        _idPortenSettings = idPortenSettings.Value;
        _logoutTokenValidator = logoutTokenValidator;
        _logoutSessionStore = logoutSessionStore;
        _logger = logger;
    }

    /// <summary>
    /// Initiates ID-Porten login via OIDC authorization code flow.
    /// The OIDC middleware handles <c>/broker/api/v1/authentication/callback</c> (CallbackPath) —
    /// do not redirect there after login; that path must only receive ID-Porten's code+state.
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = "/")
    {
        var path = ToSafeAppPath(returnUrl);
        var properties = new AuthenticationProperties
        {
            // Absolute SPA URL when SpaBaseUrl is set (local Vite); otherwise same-origin path.
            RedirectUri = _idPortenSettings.BuildSpaUrl(path)
        };
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// OIDC redirect URI for the ID-Porten authorization code flow (GET, <c>response_mode=query</c>).
    /// Handled by the OpenIdConnect middleware (<c>CallbackPath</c>) before MVC —
    /// this action exists so the path appears in OpenAPI. Register the absolute URL
    /// as <c>redirect_uri</c> on the ID-Porten client (e.g. Front Door + this path).
    /// </summary>
    [HttpPost("callback")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult CallbackPost(
        [FromForm] string? code = null,
        [FromForm] string? state = null,
        [FromForm] string? iss = null,
        [FromForm] string? error = null,
        [FromForm(Name = "error_description")] string? errorDescription = null)
    {
        _ = (code, state, iss, error, errorDescription);
        return NotFound();
    }

    /// <summary>
    /// Logs out: clears cookie and redirects through ID-Porten endsession.
    /// </summary>
    [HttpGet("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl = null)
    {
        await RevokeLocalOidcSessionAsync();
        var path = ToSafeAppPath(returnUrl ?? _idPortenSettings.PostLogoutRedirectUri);
        var properties = new AuthenticationProperties
        {
            RedirectUri = _idPortenSettings.BuildSpaUrl(path)
        };
        return SignOut(properties,
            AuthorizationConstants.EndUserCookie,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Returns current user info. Always 200 — avoids SPA login loops from 401 on the session probe.
    /// </summary>
    [HttpGet("me")]
    [AllowAnonymous]
    public async Task<IActionResult> Me()
    {
        var result = await HttpContext.AuthenticateAsync(AuthorizationConstants.EndUserCookie);
        if (!result.Succeeded || result.Principal?.Identity?.IsAuthenticated != true)
        {
            return Ok(new { authenticated = false, claims = Array.Empty<object>() });
        }

        var claims = result.Principal.Claims
            .Where(c =>
                c.Type.StartsWith("urn:altinn:")
                || c.Type is "pid" or "name" or "acr" or "sid"
                || c.Type.Contains("authnclassreference", StringComparison.OrdinalIgnoreCase))
            .Select(c => new { c.Type, c.Value });
        return Ok(new { authenticated = true, claims });
    }

    /// <summary>
    /// Front-channel logout endpoint registered with ID-Porten.
    /// Clears the session cookie when logout is initiated from another service (browser GET).
    /// </summary>
    [HttpGet("frontchannel-logout")]
    [AllowAnonymous]
    public async Task<IActionResult> FrontChannelLogout()
    {
        await RevokeLocalOidcSessionAsync();
        await HttpContext.SignOutAsync(AuthorizationConstants.EndUserCookie);
        return Ok();
    }

    /// <summary>
    /// Back-channel logout endpoint registered as <c>backchannel_logout_uri</c> on the ID-Porten client.
    /// ID-Porten POSTs a signed <c>logout_token</c> when the user signs out of another public-sector service.
    /// See https://docs.digdir.no/docs/idporten/oidc/oidc_func_backchannel_logout.html
    /// </summary>
    [HttpPost("backchannel-logout")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BackChannelLogout(
        [FromForm(Name = "logout_token")] string? logoutToken,
        CancellationToken cancellationToken)
    {
        var claims = await _logoutTokenValidator.ValidateAsync(logoutToken ?? string.Empty, cancellationToken);
        if (claims is null)
        {
            return BadRequest();
        }

        var jtiTtl = claims.ExpiresAt - DateTimeOffset.UtcNow;
        if (jtiTtl < TimeSpan.FromMinutes(1))
        {
            jtiTtl = TimeSpan.FromMinutes(5);
        }

        if (!await _logoutSessionStore.TryConsumeJtiAsync(claims.Jti, jtiTtl, cancellationToken))
        {
            // Already processed — still 204 so ID-Porten treats the call as accepted (idempotent).
            return NoContent();
        }

        await _logoutSessionStore.RevokeAsync(
            claims.Sid,
            claims.Sub,
            _idPortenSettings.SessionRevocationLifetime,
            cancellationToken);

        _logger.LogInformation("Revoked end-user session from ID-Porten back-channel logout");
        return NoContent();
    }

    private async Task RevokeLocalOidcSessionAsync()
    {
        var result = await HttpContext.AuthenticateAsync(AuthorizationConstants.EndUserCookie);
        if (!result.Succeeded)
        {
            return;
        }

        var sid = GetItem(result.Properties, OidcSessionKeys.Sid);
        var sub = GetItem(result.Properties, OidcSessionKeys.Sub);
        if (string.IsNullOrEmpty(sid) && string.IsNullOrEmpty(sub))
        {
            return;
        }

        await _logoutSessionStore.RevokeAsync(sid, sub, _idPortenSettings.SessionRevocationLifetime);
    }

    private static string? GetItem(AuthenticationProperties? properties, string key)
        => properties?.Items.TryGetValue(key, out var value) == true ? value : null;

    private string ToSafeAppPath(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        // Allow absolute URLs only when they match the configured SPA origin.
        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute)
            && !string.IsNullOrWhiteSpace(_idPortenSettings.SpaBaseUrl)
            && Uri.TryCreate(_idPortenSettings.SpaBaseUrl, UriKind.Absolute, out var spaBase)
            && string.Equals(absolute.GetLeftPart(UriPartial.Authority), spaBase.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrEmpty(absolute.PathAndQuery) ? "/" : absolute.PathAndQuery;
        }

        if (Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        return "/";
    }
}
