using Altinn.Broker.API.AltinnPlatformAuth;
using Altinn.Broker.API.AltinnPlatformAuth.Options;
using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.IdPortenDirectAuth;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.API.Controllers;

/// <summary>
/// Shared end-user authentication endpoints (session probe and Altinn platform SSO refresh).
/// ID-Porten direct login lives in <see cref="IdPortenDirectAuthController"/>.
/// </summary>
[ApiController]
[Route("broker/api/v1/authentication")]
public class AuthenticationController : ControllerBase
{
    private readonly IAltinnPlatformAuthenticationClient _platformAuthenticationClient;
    private readonly IAltinnPlatformJwtCookieReader _platformJwtCookieReader;
    private readonly AltinnPlatformAuthSettings _platformAuthSettings;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(
        IAltinnPlatformAuthenticationClient platformAuthenticationClient,
        IAltinnPlatformJwtCookieReader platformJwtCookieReader,
        IOptions<AltinnPlatformAuthSettings> platformAuthSettings,
        ILogger<AuthenticationController> logger)
    {
        _platformAuthenticationClient = platformAuthenticationClient;
        _platformJwtCookieReader = platformJwtCookieReader;
        _platformAuthSettings = platformAuthSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns current user info. Always 200 — avoids SPA login loops from 401 on the session probe.
    /// </summary>
    [HttpGet("me")]
    [AllowAnonymous]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";

        var result = await HttpContext.AuthenticateAsync(AuthorizationConstants.EndUserCookie);
        var principal = result.Succeeded && result.Principal?.Identity?.IsAuthenticated == true
            ? result.Principal
            : await _platformJwtCookieReader.ReadAuthenticatedPrincipalAsync(HttpContext, cancellationToken);

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return Ok(new { authenticated = false, claims = Array.Empty<object>() });
        }

        // Exclude sensitive identifiers (e.g. pid). SPA only needs display name and Altinn party claims.
        var claims = principal.Claims
            .Where(c =>
                c.Type.StartsWith("urn:altinn:", StringComparison.Ordinal)
                || c.Type is "name")
            .Select(c => new { c.Type, c.Value });
        return Ok(new { authenticated = true, claims });
    }

    /// <summary>
    /// Refreshes the Altinn platform JWT using the shared portal session cookie (e.g. on *.altinn.no)
    /// and stores it in an httpOnly runtime cookie for Broker API calls.
    /// </summary>
    [HttpGet("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        try
        {
            var token = await _platformAuthenticationClient.RefreshTokenAsync(HttpContext, cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(_platformAuthSettings.JwtCookieName))
            {
                return BadRequest();
            }

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                IsEssential = true,
                SameSite = AuthCookieDefaults.SameSite
            };

            if (!string.IsNullOrWhiteSpace(_platformAuthSettings.CookieDomain))
            {
                cookieOptions.Domain = _platformAuthSettings.CookieDomain;
            }

            Response.Cookies.Append(_platformAuthSettings.JwtCookieName, token, cookieOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Altinn platform token refresh failed.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Ok();
    }
}
