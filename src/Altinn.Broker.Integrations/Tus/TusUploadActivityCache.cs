using Microsoft.Extensions.Caching.Distributed;

namespace Altinn.Broker.Integrations.Tus;

public interface ITusUploadActivityCache
{
    Task RecordActivityAsync(Guid fileTransferId, CancellationToken cancellationToken);
}

public sealed class TusUploadActivityCache(IDistributedCache cache) : ITusUploadActivityCache
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    private static string BuildKey(Guid fileTransferId) => $"tus-upload-activity:{fileTransferId}";

    public Task RecordActivityAsync(Guid fileTransferId, CancellationToken cancellationToken)
        => cache.SetStringAsync(
            BuildKey(fileTransferId),
            DateTimeOffset.UtcNow.ToString("O"),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration
            },
            cancellationToken);
}
