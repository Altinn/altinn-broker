using System.Globalization;

using Microsoft.Extensions.Caching.Distributed;

namespace Altinn.Broker.API.IdPortenDirectAuth;

public sealed class OidcBackChannelLogoutSessionStore(IDistributedCache cache) : IOidcBackChannelLogoutSessionStore
{
    public async Task<bool> TryConsumeJtiAsync(string jti, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        var key = JtiKey(jti);
        var existing = await cache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrEmpty(existing))
        {
            return false;
        }

        await cache.SetStringAsync(key, "1", CacheOptions(timeToLive), cancellationToken);
        return true;
    }

    public async Task RevokeAsync(string? sid, string? sub, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        var options = CacheOptions(timeToLive);
        var revokedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        // Prefer sid; only fall back to sub when no session id exists.
        if (!string.IsNullOrEmpty(sid))
        {
            await cache.SetStringAsync(SidKey(sid), revokedAt, options, cancellationToken);
            return;
        }

        if (!string.IsNullOrEmpty(sub))
        {
            await cache.SetStringAsync(SubKey(sub), revokedAt, options, cancellationToken);
        }
    }

    public async Task<bool> IsRevokedAsync(
        string? sid,
        string? sub,
        DateTimeOffset? cookieIssuedUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(sid))
        {
            var sidValue = await cache.GetStringAsync(SidKey(sid), cancellationToken);
            if (IsActiveRevocation(sidValue, cookieIssuedUtc))
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(sub))
        {
            var subValue = await cache.GetStringAsync(SubKey(sub), cancellationToken);
            if (IsActiveRevocation(subValue, cookieIssuedUtc))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A revocation applies when present and the cookie was issued at or before the revocation time.
    /// Cookies issued after logout (re-login) remain valid.
    /// </summary>
    private static bool IsActiveRevocation(string? storedValue, DateTimeOffset? cookieIssuedUtc)
    {
        if (string.IsNullOrEmpty(storedValue))
        {
            return false;
        }

        if (cookieIssuedUtc is null)
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(storedValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var revokedAt))
        {
            // Legacy "1" entries: treat as revoked.
            return true;
        }

        return cookieIssuedUtc <= revokedAt;
    }

    private static DistributedCacheEntryOptions CacheOptions(TimeSpan timeToLive) => new()
    {
        AbsoluteExpirationRelativeToNow = timeToLive
    };

    private static string SidKey(string sid) => $"oidc-logout:sid:{sid}";
    private static string SubKey(string sub) => $"oidc-logout:sub:{sub}";
    private static string JtiKey(string jti) => $"oidc-logout:jti:{jti}";
}
