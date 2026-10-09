namespace Altinn.Broker.API.IdPortenDirectAuth;

/// <summary>
/// Tokens returned by an ID-Porten refresh_token grant. ID-Porten rotates refresh tokens,
/// so <see cref="RefreshToken"/> replaces the one that was redeemed.
/// </summary>
public sealed record IdPortenTokens(string AccessToken, string RefreshToken);

public interface IIdPortenTokenRefreshService
{
    /// <summary>
    /// Redeems a refresh token at ID-Porten.
    /// Returns null when the token is spent, revoked, or ID-Porten is unavailable.
    /// </summary>
    Task<IdPortenTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns rotated tokens if a recent refresh of <paramref name="refreshToken"/> is already
    /// cached. Does not call ID-Porten. Used when writing the auth cookie so a long-lived request
    /// cannot Set-Cookie a spent refresh token over a newer session.
    /// </summary>
    Task<IdPortenTokens?> GetCachedRotationAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}
