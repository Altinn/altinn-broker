using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

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

public sealed class TusUploadProgressCache(
    IDistributedCache cache,
    ILogger<TusUploadProgressCache> logger) : ITusUploadProgressCache
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string BuildKey(string fileId) => $"tus-upload-progress:{fileId}";

    public async Task<TusUploadProgressSnapshot?> GetAsync(string fileId, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var json = await cache.GetStringAsync(BuildKey(fileId), cancellationToken);
        var redisMs = sw.ElapsedMilliseconds;
        if (string.IsNullOrWhiteSpace(json))
        {
            logger.LogDebug(
                "TUS timing progressCache.Get +{RedisMs}ms total {TotalMs}ms fileId={FileId} hit=false",
                redisMs,
                sw.ElapsedMilliseconds,
                fileId);
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<TusUploadProgressSnapshot>(json, JsonOptions);
        logger.LogDebug(
            "TUS timing progressCache.Get +{RedisMs}ms total {TotalMs}ms fileId={FileId} hit=true offset={Offset}",
            redisMs,
            sw.ElapsedMilliseconds,
            fileId,
            snapshot?.CommittedOffset);
        return snapshot;
    }

    public async Task SaveAsync(string fileId, TusUploadProgressSnapshot snapshot, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await cache.SetStringAsync(
            BuildKey(fileId),
            JsonSerializer.Serialize(snapshot, JsonOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration
            },
            cancellationToken);
        logger.LogDebug(
            "TUS timing progressCache.Save +{RedisMs}ms total {TotalMs}ms fileId={FileId} offset={Offset} blockCount={BlockCount}",
            sw.ElapsedMilliseconds,
            sw.ElapsedMilliseconds,
            fileId,
            snapshot.CommittedOffset,
            snapshot.BlockIds.Count);
    }

    public Task RemoveAsync(string fileId, CancellationToken cancellationToken)
        => cache.RemoveAsync(BuildKey(fileId), cancellationToken);
}
