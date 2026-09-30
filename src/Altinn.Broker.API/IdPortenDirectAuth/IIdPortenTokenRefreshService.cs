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
}
