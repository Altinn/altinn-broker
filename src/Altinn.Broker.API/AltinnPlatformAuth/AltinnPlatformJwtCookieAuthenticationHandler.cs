using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;

using Altinn.Broker.API.Configuration;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.API.AltinnPlatformAuth;

/// <summary>
/// Authenticates requests using the Altinn platform JWT httpOnly cookie set by /authentication/refresh.
/// </summary>
public sealed class AltinnPlatformJwtCookieAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IAltinnPlatformJwtCookieReader _cookieReader;

    public AltinnPlatformJwtCookieAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAltinnPlatformJwtCookieReader cookieReader)
        : base(options, logger, encoder)
    {
        _cookieReader = cookieReader;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = await _cookieReader.ReadAuthenticatedPrincipalAsync(Context, Context.RequestAborted);
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return AuthenticateResult.NoResult();
        }

        var identity = (ClaimsIdentity)principal.Identity!;
        if (identity.AuthenticationType != AuthorizationConstants.AltinnPlatformJwtCookie)
        {
            identity = new ClaimsIdentity(
                principal.Claims,
                AuthorizationConstants.AltinnPlatformJwtCookie,
                identity.NameClaimType,
                identity.RoleClaimType);
            principal = new ClaimsPrincipal(identity);
        }

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
