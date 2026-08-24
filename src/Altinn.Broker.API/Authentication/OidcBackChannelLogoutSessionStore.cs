using Microsoft.Extensions.Caching.Distributed;

namespace Altinn.Broker.API.Authentication;

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
        if (!string.IsNullOrEmpty(sid))
        {
            await cache.SetStringAsync(SidKey(sid), "1", options, cancellationToken);
        }

        if (!string.IsNullOrEmpty(sub))
        {
            await cache.SetStringAsync(SubKey(sub), "1", options, cancellationToken);
        }
    }

    public async Task<bool> IsRevokedAsync(string? sid, string? sub, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(sid) && !string.IsNullOrEmpty(await cache.GetStringAsync(SidKey(sid), cancellationToken)))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(sub) && !string.IsNullOrEmpty(await cache.GetStringAsync(SubKey(sub), cancellationToken)))
        {
            return true;
        }

        return false;
    }

    private static DistributedCacheEntryOptions CacheOptions(TimeSpan timeToLive) => new()
    {
        AbsoluteExpirationRelativeToNow = timeToLive
    };

    private static string SidKey(string sid) => $"oidc-logout:sid:{sid}";
    private static string SubKey(string sub) => $"oidc-logout:sub:{sub}";
    private static string JtiKey(string jti) => $"oidc-logout:jti:{jti}";
}
