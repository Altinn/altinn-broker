using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.IdPortenDirectAuth.Options;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.API.IdPortenDirectAuth;

/// <summary>
/// ID-Porten OIDC login flow where Broker acts as confidential client (direct auth).
/// For shared Altinn portal session on *.altinn.no, use <see cref="Controllers.AuthenticationController.Refresh"/> instead.
/// </summary>
[ApiController]
[Route("broker/api/v1/authentication")]
public class IdPortenDirectAuthController : ControllerBase
{
    private readonly IdPortenDirectAuthSettings _settings;
    private readonly IOidcLogoutTokenValidator _logoutTokenValidator;
    private readonly IOidcBackChannelLogoutSessionStore _logoutSessionStore;
    private readonly ILogger<IdPortenDirectAuthController> _logger;

    public IdPortenDirectAuthController(
        IOptions<IdPortenDirectAuthSettings> settings,
        IOidcLogoutTokenValidator logoutTokenValidator,
        IOidcBackChannelLogoutSessionStore logoutSessionStore,
        ILogger<IdPortenDirectAuthController> logger)
    {
        _settings = settings.Value;
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
            RedirectUri = _settings.BuildSpaUrl(path)
        };
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// OIDC redirect URI when ID-Porten uses <c>response_mode=form_post</c> (browser POST).
    /// Handled by OpenIdConnect middleware before MVC — documented for APIM/OpenAPI.
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
        var path = ToSafeAppPath(returnUrl ?? IdPortenDirectAuthDefaults.PostLogoutRedirectUri);
        var properties = new AuthenticationProperties
        {
            RedirectUri = _settings.BuildSpaUrl(path)
        };
        return SignOut(properties,
            AuthorizationConstants.EndUserCookie,
            OpenIdConnectDefaults.AuthenticationScheme);
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
            return NoContent();
        }

        await _logoutSessionStore.RevokeAsync(
            claims.Sid,
            string.IsNullOrEmpty(claims.Sid) ? claims.Sub : null,
            IdPortenDirectAuthDefaults.SessionRevocationLifetime,
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

        await _logoutSessionStore.RevokeAsync(
            sid,
            string.IsNullOrEmpty(sid) ? sub : null,
            IdPortenDirectAuthDefaults.SessionRevocationLifetime);
    }

    private static string? GetItem(AuthenticationProperties? properties, string key)
        => properties?.Items.TryGetValue(key, out var value) == true ? value : null;

    private string ToSafeAppPath(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute)
            && !string.IsNullOrWhiteSpace(_settings.SpaBaseUrl)
            && Uri.TryCreate(_settings.SpaBaseUrl, UriKind.Absolute, out var spaBase)
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
