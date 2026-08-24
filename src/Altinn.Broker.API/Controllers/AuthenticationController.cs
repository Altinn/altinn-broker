using Altinn.Broker.API.Configuration;
using Altinn.Broker.Core.Options;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.API.Controllers;

[ApiController]
[Route("authentication")]
public class AuthenticationController : ControllerBase
{
    private readonly IdPortenSettings _idPortenSettings;

    public AuthenticationController(IOptions<IdPortenSettings> idPortenSettings)
    {
        _idPortenSettings = idPortenSettings.Value;
    }

    /// <summary>
    /// Initiates ID-Porten login via OIDC authorization code flow.
    /// The OIDC middleware handles <c>/authentication/callback</c> (CallbackPath) —
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
    /// Logs out: clears cookie and redirects through ID-Porten endsession.
    /// </summary>
    [HttpGet("logout")]
    [AllowAnonymous]
    public IActionResult Logout([FromQuery] string? returnUrl = null)
    {
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
                || c.Type is "pid" or "name" or "acr"
                || c.Type.Contains("authnclassreference", StringComparison.OrdinalIgnoreCase))
            .Select(c => new { c.Type, c.Value });
        return Ok(new { authenticated = true, claims });
    }

    /// <summary>
    /// Front-channel logout endpoint registered with ID-Porten.
    /// Clears the session cookie when logout is initiated from another service.
    /// </summary>
    [HttpGet("frontchannel-logout")]
    [AllowAnonymous]
    public async Task<IActionResult> FrontChannelLogout()
    {
        await HttpContext.SignOutAsync(AuthorizationConstants.EndUserCookie);
        return Ok();
    }

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
