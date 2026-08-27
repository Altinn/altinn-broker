namespace Altinn.Broker.API.AltinnPlatformAuth;

public interface IAltinnPlatformAuthenticationClient
{
    /// <summary>
    /// Calls Altinn platform <c>GET /authentication/api/v1/refresh</c>,
    /// forwarding the browser's cookies. Returns a refreshed Altinn JWT or null.
    /// </summary>
    Task<string?> RefreshTokenAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
