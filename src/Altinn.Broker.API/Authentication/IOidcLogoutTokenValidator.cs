namespace Altinn.Broker.API.Authentication;

public sealed record OidcLogoutTokenClaims(string? Sid, string? Sub, string Jti, DateTimeOffset ExpiresAt);

public interface IOidcLogoutTokenValidator
{
    Task<OidcLogoutTokenClaims?> ValidateAsync(string logoutToken, CancellationToken cancellationToken = default);
}
