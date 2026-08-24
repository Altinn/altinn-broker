namespace Altinn.Broker.API.Authentication;

public interface IOidcBackChannelLogoutSessionStore
{
    /// <summary>
    /// Records a logout_token jti. Returns false if it was already processed (replay).
    /// </summary>
    Task<bool> TryConsumeJtiAsync(string jti, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    Task RevokeAsync(string? sid, string? sub, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    Task<bool> IsRevokedAsync(string? sid, string? sub, CancellationToken cancellationToken = default);
}
