using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Caching.Distributed;

namespace Altinn.Broker.Integrations.Tus;

public sealed class TusUploadActivityCache(IDistributedCache cache) : ITusUploadActivityCache
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(25);

    private static string BuildKey(Guid fileTransferId) => $"tus-upload-activity:{fileTransferId:D}";

    public Task RecordActivityAsync(Guid fileTransferId, CancellationToken cancellationToken = default)
        => cache.SetStringAsync(
            BuildKey(fileTransferId),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration
            },
            cancellationToken);

    public async Task<bool> HasRecentActivityAsync(
        Guid fileTransferId,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var value = await cache.GetStringAsync(BuildKey(fileTransferId), cancellationToken);
        if (!long.TryParse(value, out var lastActivityMs))
        {
            return false;
        }

        var lastActivity = DateTimeOffset.FromUnixTimeMilliseconds(lastActivityMs);
        return DateTimeOffset.UtcNow - lastActivity <= window;
    }
}
