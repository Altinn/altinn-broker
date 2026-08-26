namespace Altinn.Broker.API.IdPortenDirectAuth;

public interface IOidcBackChannelLogoutSessionStore
{
    /// <summary>
    /// Records a logout_token jti. Returns false if it was already processed (replay).
    /// </summary>
    Task<bool> TryConsumeJtiAsync(string jti, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes by <paramref name="sid"/> when present; otherwise by <paramref name="sub"/>.
    /// Only one key is stored per logout.
    /// </summary>
    Task RevokeAsync(string? sid, string? sub, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when the session is revoked. For sub-based (and sid) revocations, cookies
    /// issued after the revocation instant remain valid (<paramref name="cookieIssuedUtc"/>).
    /// </summary>
    Task<bool> IsRevokedAsync(
        string? sid,
        string? sub,
        DateTimeOffset? cookieIssuedUtc = null,
        CancellationToken cancellationToken = default);
}
