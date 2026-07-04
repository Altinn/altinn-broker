using Altinn.Broker.Application;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Caching.Hybrid;

namespace Altinn.Broker.Integrations.Tus;

public sealed class TusUploadActivityCache(HybridCache cache) : ITusUploadActivityCache
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(25);
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = CacheExpiration,
        Flags = HybridCacheEntryFlags.DisableLocalCache
    };

    private static string BuildKey(Guid fileTransferId) => $"tus-upload-activity:{fileTransferId:D}";

    public Task RecordActivityAsync(Guid fileTransferId, CancellationToken cancellationToken = default)
        => cache.SetStringAsync(
            BuildKey(fileTransferId),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            CacheOptions,
            cancellationToken);

    public async Task<bool> HasRecentActivityAsync(
        Guid fileTransferId,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var value = await cache.GetOptionalStringAsync(BuildKey(fileTransferId), CacheOptions, cancellationToken);
        if (!long.TryParse(value, out var lastActivityMs))
        {
            return false;
        }

        var lastActivity = DateTimeOffset.FromUnixTimeMilliseconds(lastActivityMs);
        return DateTimeOffset.UtcNow - lastActivity <= window;
    }
}
