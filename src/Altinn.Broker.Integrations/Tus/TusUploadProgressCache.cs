using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;

namespace Altinn.Broker.Integrations.Tus;

public sealed record TusUploadProgressSnapshot(
    long UploadLength,
    long AcceptedOffset,
    long CommittedOffset,
    long NextBlockIndex,
    List<string> BlockIds);

public interface ITusUploadProgressCache
{
    Task<TusUploadProgressSnapshot?> GetAsync(string fileId, CancellationToken cancellationToken);

    Task SaveAsync(string fileId, TusUploadProgressSnapshot snapshot, CancellationToken cancellationToken);

    Task RemoveAsync(string fileId, CancellationToken cancellationToken);
}

public sealed class TusUploadProgressCache(IDistributedCache cache) : ITusUploadProgressCache
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    private static string BuildKey(string fileId) => $"tus-upload-progress:{fileId}";

    public async Task<TusUploadProgressSnapshot?> GetAsync(string fileId, CancellationToken cancellationToken)
    {
        var json = await cache.GetStringAsync(BuildKey(fileId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<TusUploadProgressSnapshot>(json);
    }

    public Task SaveAsync(string fileId, TusUploadProgressSnapshot snapshot, CancellationToken cancellationToken)
        => cache.SetStringAsync(
            BuildKey(fileId),
            JsonSerializer.Serialize(snapshot),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration
            },
            cancellationToken);

    public Task RemoveAsync(string fileId, CancellationToken cancellationToken)
        => cache.RemoveAsync(BuildKey(fileId), cancellationToken);
}
