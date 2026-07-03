using System.Collections.Concurrent;
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

    private readonly ConcurrentDictionary<string, TusUploadProgressSnapshot> _localSnapshots =
        new(StringComparer.OrdinalIgnoreCase);

    private static string BuildKey(string fileId) => $"tus-upload-progress:{fileId}";

    public async Task<TusUploadProgressSnapshot?> GetAsync(string fileId, CancellationToken cancellationToken)
    {
        if (_localSnapshots.TryGetValue(fileId, out var localSnapshot))
        {
            return localSnapshot;
        }

        var json = await cache.GetStringAsync(BuildKey(fileId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<TusUploadProgressSnapshot>(json);
        if (snapshot is not null)
        {
            _localSnapshots[fileId] = snapshot;
        }

        return snapshot;
    }

    public async Task SaveAsync(string fileId, TusUploadProgressSnapshot snapshot, CancellationToken cancellationToken)
    {
        _localSnapshots[fileId] = snapshot;
        await cache.SetStringAsync(
            BuildKey(fileId),
            JsonSerializer.Serialize(snapshot),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration
            },
            cancellationToken);
    }

    public async Task RemoveAsync(string fileId, CancellationToken cancellationToken)
    {
        _localSnapshots.TryRemove(fileId, out _);
        await cache.RemoveAsync(BuildKey(fileId), cancellationToken);
    }
}
