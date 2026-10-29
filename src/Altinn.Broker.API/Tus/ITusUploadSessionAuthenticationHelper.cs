using System.Security.Claims;

namespace Altinn.Broker.API.Tus;

public interface ITusUploadSessionAuthenticationHelper
{
    Task<ClaimsPrincipal?> TryValidateExpiredTokenForActiveUploadAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken);

    Task<ClaimsPrincipal?> TryAcceptExpiredAltinnCookieForActiveUploadAsync(
        HttpContext httpContext,
        string altinnToken,
        string? sid,
        ClaimsPrincipal? existingPrincipal,
        CancellationToken cancellationToken);
}
