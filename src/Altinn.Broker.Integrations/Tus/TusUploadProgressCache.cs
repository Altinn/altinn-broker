using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

using Altinn.Broker.Application;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace Altinn.Broker.Integrations.Tus;

public sealed record TusUploadProgressSnapshot(
    long UploadLength,
    long AcceptedOffset,
    long CommittedOffset,
    long NextBlockIndex);

public interface ITusUploadProgressCache
{
    Task<TusUploadProgressSnapshot?> GetAsync(string fileId, CancellationToken cancellationToken);

    Task InitializeAsync(string fileId, long uploadLength, CancellationToken cancellationToken);

    /// <summary>
    /// Reserves the next <paramref name="blockCount"/> block indices atomically. A chunk that straddles
    /// a stripe boundary stages one block per stripe it touches.
    /// </summary>
    Task<TusAcceptChunkResult> TryAcceptChunkAsync(
        string fileId,
        long expectedOffset,
        int chunkLength,
        int blockCount,
        CancellationToken cancellationToken);

    Task IncrementCommittedOffsetAsync(string fileId, long chunkLength, CancellationToken cancellationToken);

    Task SaveAsync(string fileId, TusUploadProgressSnapshot snapshot, CancellationToken cancellationToken);

    Task RemoveAsync(string fileId, CancellationToken cancellationToken);
}

public sealed class TusUploadProgressCache(
    HybridCache hybridCache,
    ILogger<TusUploadProgressCache> logger,
    IConnectionMultiplexer? redis = null) : ITusUploadProgressCache
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HybridCacheEntryOptions HybridProgressOptions = new()
    {
        Expiration = CacheExpiration,
        Flags = HybridCacheEntryFlags.DisableLocalCache
    };

    private static readonly ConcurrentDictionary<string, InMemoryProgressEntry> InMemoryProgress =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IDatabase? _database = redis?.GetDatabase();

    private const string UploadLengthField = "uploadLength";
    private const string AcceptedOffsetField = "acceptedOffset";
    private const string CommittedOffsetField = "committedOffset";
    private const string NextBlockIndexField = "nextBlockIndex";

    private const string TryAcceptChunkScript = """
        local expected = tonumber(ARGV[1])
        local chunkLen = tonumber(ARGV[2])
        local ttl = tonumber(ARGV[3])
        local blockCount = tonumber(ARGV[4])

        local accepted = redis.call('HGET', KEYS[1], 'acceptedOffset')
        if not accepted then
            return {0, 0}
        end

        accepted = tonumber(accepted)
        local uploadLen = tonumber(redis.call('HGET', KEYS[1], 'uploadLength'))

        if accepted ~= expected then
            return {2, accepted}
        end

        local newAccepted = accepted + chunkLen
        if newAccepted > uploadLen then
            return {3, accepted}
        end

        local blockIndex = tonumber(redis.call('HGET', KEYS[1], 'nextBlockIndex') or '0')
        redis.call('HSET', KEYS[1], 'acceptedOffset', newAccepted, 'nextBlockIndex', blockIndex + blockCount)
        redis.call('EXPIRE', KEYS[1], ttl)
        return {1, accepted, newAccepted, blockIndex}
        """;

    public async Task<TusUploadProgressSnapshot?> GetAsync(string fileId, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        if (_database is not null)
        {
            // Redis hash is authoritative across replicas. Do not read HybridCache first:
            // per-replica L1 can hold a stale offset and cause 409 storms without session affinity.
            var snapshot = await ReadHashAsync(fileId, cancellationToken);
            if (snapshot is not null)
            {
                LogGetHit(sw, fileId, snapshot.AcceptedOffset, source: "redis");
                return snapshot;
            }

            var migrated = await TryMigrateLegacyJsonAsync(fileId, cancellationToken);
            if (migrated is not null)
            {
                LogGetHit(sw, fileId, migrated.AcceptedOffset, source: "legacy");
                return migrated;
            }

            LogGetMiss(sw, fileId);
            return null;
        }

        var cached = await hybridCache.GetOptionalAsync<TusUploadProgressSnapshot>(
            BuildHybridKey(fileId),
            HybridProgressOptions,
            cancellationToken);
        if (cached is not null)
        {
            LogGetHit(sw, fileId, cached.AcceptedOffset, source: "hybrid");
            return cached;
        }

        if (InMemoryProgress.TryGetValue(fileId, out var entry))
        {
            TusUploadProgressSnapshot snapshot;
            lock (entry.SyncRoot)
            {
                snapshot = entry.ToSnapshot();
            }

            await SetHybridProgressAsync(fileId, snapshot, cancellationToken);
            LogGetHit(sw, fileId, snapshot.AcceptedOffset, source: "memory");
            return snapshot;
        }

        LogGetMiss(sw, fileId);
        return null;
    }

    public async Task InitializeAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
    {
        var snapshot = new TusUploadProgressSnapshot(uploadLength, 0, 0, 0);
        if (_database is not null)
        {
            await WriteHashAsync(fileId, snapshot, cancellationToken);
        }
        else
        {
            InMemoryProgress[fileId] = new InMemoryProgressEntry(uploadLength);
            await SetHybridProgressAsync(fileId, snapshot, cancellationToken);
        }
    }

    public async Task<TusAcceptChunkResult> TryAcceptChunkAsync(
        string fileId,
        long expectedOffset,
        int chunkLength,
        int blockCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockCount);

        TusAcceptChunkResult result;
        if (_database is not null)
        {
            result = await TryAcceptChunkRedisAsync(fileId, expectedOffset, chunkLength, blockCount, cancellationToken);
        }
        else
        {
            result = TryAcceptChunkInMemory(fileId, expectedOffset, chunkLength, blockCount);
        }

        if (result.Status == TusAcceptChunkStatus.Accepted && _database is null)
        {
            await RefreshHybridProgressAsync(fileId, cancellationToken);
        }

        return result;
    }

    public async Task IncrementCommittedOffsetAsync(string fileId, long chunkLength, CancellationToken cancellationToken)
    {
        if (_database is not null)
        {
            await IncrementCommittedOffsetRedisAsync(fileId, chunkLength, cancellationToken);
        }
        else if (InMemoryProgress.TryGetValue(fileId, out var entry))
        {
            lock (entry.SyncRoot)
            {
                entry.CommittedOffset += chunkLength;
            }
        }

        if (_database is null)
        {
            await RefreshHybridProgressAsync(fileId, cancellationToken);
        }
    }

    public async Task SaveAsync(string fileId, TusUploadProgressSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (_database is not null)
        {
            await WriteHashAsync(fileId, snapshot, cancellationToken);
        }
        else
        {
            InMemoryProgress.AddOrUpdate(
                fileId,
                _ => InMemoryProgressEntry.FromSnapshot(snapshot),
                (_, existing) =>
                {
                    lock (existing.SyncRoot)
                    {
                        existing.UploadLength = snapshot.UploadLength;
                        existing.AcceptedOffset = snapshot.AcceptedOffset;
                        existing.CommittedOffset = snapshot.CommittedOffset;
                        existing.NextBlockIndex = snapshot.NextBlockIndex;
                    }

                    return existing;
                });
            await SetHybridProgressAsync(fileId, snapshot, cancellationToken);
        }
    }

    public async Task RemoveAsync(string fileId, CancellationToken cancellationToken)
    {
        if (_database is not null)
        {
            await _database.KeyDeleteAsync(BuildHashKey(fileId));
        }

        InMemoryProgress.TryRemove(fileId, out _);
        await hybridCache.RemoveKeyAsync(BuildHybridKey(fileId), cancellationToken);
        await hybridCache.RemoveKeyAsync(BuildLegacyKey(fileId), cancellationToken);
    }

    private async Task RefreshHybridProgressAsync(string fileId, CancellationToken cancellationToken)
    {
        if (_database is not null)
        {
            var snapshot = await ReadHashAsync(fileId, cancellationToken);
            if (snapshot is not null)
            {
                await SetHybridProgressAsync(fileId, snapshot, cancellationToken);
            }

            return;
        }

        if (InMemoryProgress.TryGetValue(fileId, out var entry))
        {
            TusUploadProgressSnapshot snapshot;
            lock (entry.SyncRoot)
            {
                snapshot = entry.ToSnapshot();
            }

            await SetHybridProgressAsync(fileId, snapshot, cancellationToken);
        }
    }

    private Task SetHybridProgressAsync(
        string fileId,
        TusUploadProgressSnapshot snapshot,
        CancellationToken cancellationToken)
        => hybridCache.SetAsync(
            BuildHybridKey(fileId),
            snapshot,
            HybridProgressOptions,
            cancellationToken: cancellationToken).AsTask();

    private async Task<TusUploadProgressSnapshot?> ReadHashAsync(string fileId, CancellationToken cancellationToken)
    {
        var entries = await _database!.HashGetAllAsync(BuildHashKey(fileId));
        if (entries.Length == 0)
        {
            return null;
        }

        return ParseHashEntries(entries);
    }

    private async Task WriteHashAsync(string fileId, TusUploadProgressSnapshot snapshot, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var key = BuildHashKey(fileId);
        var entries = new HashEntry[]
        {
            new(UploadLengthField, snapshot.UploadLength),
            new(AcceptedOffsetField, snapshot.AcceptedOffset),
            new(CommittedOffsetField, snapshot.CommittedOffset),
            new(NextBlockIndexField, snapshot.NextBlockIndex)
        };

        await _database!.HashSetAsync(key, entries);
        await _database.KeyExpireAsync(key, CacheExpiration);
        logger.LogDebug(
            "TUS timing progressCache.Save +{RedisMs}ms fileId={FileId} accepted={AcceptedOffset} committed={CommittedOffset}",
            sw.ElapsedMilliseconds,
            fileId,
            snapshot.AcceptedOffset,
            snapshot.CommittedOffset);
    }

    private async Task<TusAcceptChunkResult> TryAcceptChunkRedisAsync(
        string fileId,
        long expectedOffset,
        int chunkLength,
        int blockCount,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var result = (RedisResult[]?)await _database!.ScriptEvaluateAsync(
            TryAcceptChunkScript,
            [BuildHashKey(fileId)],
            [(RedisValue)expectedOffset, chunkLength, (int)CacheExpiration.TotalSeconds, blockCount]);

        var parsed = ParseAcceptChunkResult(result);
        logger.LogDebug(
            "TUS timing progressCache.TryAcceptChunk +{RedisMs}ms fileId={FileId} status={Status} expected={ExpectedOffset} new={NewOffset}",
            sw.ElapsedMilliseconds,
            fileId,
            parsed.Status,
            expectedOffset,
            parsed.NewAcceptedOffset);
        return parsed;
    }

    private async Task IncrementCommittedOffsetRedisAsync(
        string fileId,
        long chunkLength,
        CancellationToken cancellationToken)
    {
        var key = BuildHashKey(fileId);
        if (!await _database!.KeyExistsAsync(key))
        {
            return;
        }

        if (!await _database.HashExistsAsync(key, UploadLengthField))
        {
            return;
        }

        await _database.HashIncrementAsync(key, CommittedOffsetField, chunkLength);
        await _database.KeyExpireAsync(key, CacheExpiration);
    }

    private async Task<TusUploadProgressSnapshot?> TryMigrateLegacyJsonAsync(
        string fileId,
        CancellationToken cancellationToken)
    {
        var json = await hybridCache.GetOptionalStringAsync(BuildLegacyKey(fileId), cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!TryParseLegacySnapshot(root, out var snapshot))
            {
                logger.LogWarning("Failed to migrate legacy TUS progress snapshot for file id {FileId}", fileId);
                return null;
            }

            await WriteHashAsync(fileId, snapshot, cancellationToken);
            await hybridCache.RemoveAsync(BuildLegacyKey(fileId), cancellationToken);
            return snapshot;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to migrate legacy TUS progress snapshot for file id {FileId}", fileId);
            return null;
        }
    }

    private static bool TryParseLegacySnapshot(JsonElement root, out TusUploadProgressSnapshot snapshot)
    {
        snapshot = default!;
        if (!root.TryGetProperty("UploadLength", out var uploadLengthElement)
            || !uploadLengthElement.TryGetInt64(out var uploadLength))
        {
            return false;
        }

        if (!root.TryGetProperty("CommittedOffset", out var committedOffsetElement)
            || !committedOffsetElement.TryGetInt64(out var committedOffset))
        {
            return false;
        }

        long acceptedOffset;
        if (root.TryGetProperty("AcceptedOffset", out var acceptedOffsetElement))
        {
            if (!acceptedOffsetElement.TryGetInt64(out acceptedOffset))
            {
                return false;
            }
        }
        else
        {
            acceptedOffset = committedOffset;
        }

        var nextBlockIndex = 0L;
        if (root.TryGetProperty("NextBlockIndex", out var nextBlockIndexElement)
            && !nextBlockIndexElement.TryGetInt64(out nextBlockIndex))
        {
            return false;
        }

        snapshot = new TusUploadProgressSnapshot(uploadLength, acceptedOffset, committedOffset, nextBlockIndex);
        return true;
    }

    private static TusAcceptChunkResult TryAcceptChunkInMemory(
        string fileId,
        long expectedOffset,
        int chunkLength,
        int blockCount)
    {
        var entry = InMemoryProgress.GetOrAdd(fileId, _ => new InMemoryProgressEntry(uploadLength: 0));
        lock (entry.SyncRoot)
        {
            if (entry.UploadLength == 0)
            {
                return new TusAcceptChunkResult(TusAcceptChunkStatus.NotFound, 0, 0, 0);
            }

            if (entry.AcceptedOffset != expectedOffset)
            {
                return new TusAcceptChunkResult(
                    TusAcceptChunkStatus.Conflict,
                    entry.AcceptedOffset,
                    entry.AcceptedOffset,
                    entry.NextBlockIndex);
            }

            var newAccepted = entry.AcceptedOffset + chunkLength;
            if (newAccepted > entry.UploadLength)
            {
                return new TusAcceptChunkResult(
                    TusAcceptChunkStatus.Overflow,
                    entry.AcceptedOffset,
                    entry.AcceptedOffset,
                    entry.NextBlockIndex);
            }

            var blockIndex = entry.NextBlockIndex;
            entry.AcceptedOffset = newAccepted;
            entry.NextBlockIndex += blockCount;
            return new TusAcceptChunkResult(
                TusAcceptChunkStatus.Accepted,
                expectedOffset,
                newAccepted,
                blockIndex);
        }
    }

    private static TusAcceptChunkResult ParseAcceptChunkResult(RedisResult[]? result)
    {
        if (result is null || result.Length == 0)
        {
            return new TusAcceptChunkResult(TusAcceptChunkStatus.NotFound, 0, 0, 0);
        }

        var statusCode = (int)result[0];
        return statusCode switch
        {
            1 => new TusAcceptChunkResult(
                TusAcceptChunkStatus.Accepted,
                (long)result[1],
                (long)result[2],
                (long)result[3]),
            2 => new TusAcceptChunkResult(TusAcceptChunkStatus.Conflict, (long)result[1], (long)result[1], 0),
            3 => new TusAcceptChunkResult(TusAcceptChunkStatus.Overflow, (long)result[1], (long)result[1], 0),
            _ => new TusAcceptChunkResult(TusAcceptChunkStatus.NotFound, 0, 0, 0)
        };
    }

    private static TusUploadProgressSnapshot ParseHashEntries(HashEntry[] entries)
    {
        long uploadLength = 0;
        long acceptedOffset = 0;
        long committedOffset = 0;
        long nextBlockIndex = 0;

        foreach (var entry in entries)
        {
            if (!entry.Value.HasValue)
            {
                continue;
            }

            switch (entry.Name.ToString())
            {
                case UploadLengthField:
                    uploadLength = (long)entry.Value;
                    break;
                case AcceptedOffsetField:
                    acceptedOffset = (long)entry.Value;
                    break;
                case CommittedOffsetField:
                    committedOffset = (long)entry.Value;
                    break;
                case NextBlockIndexField:
                    nextBlockIndex = (long)entry.Value;
                    break;
            }
        }

        return new TusUploadProgressSnapshot(uploadLength, acceptedOffset, committedOffset, nextBlockIndex);
    }

    private static string BuildHybridKey(string fileId) => $"tus-upload-progress:hc:{fileId}";

    private static RedisKey BuildHashKey(string fileId) => $"tus-upload-progress:v2:{fileId}";

    private static string BuildLegacyKey(string fileId) => $"tus-upload-progress:{fileId}";

    private void LogGetHit(Stopwatch sw, string fileId, long offset, string source)
        => logger.LogDebug(
            "TUS timing progressCache.Get +{RedisMs}ms fileId={FileId} hit=true source={Source} offset={Offset}",
            sw.ElapsedMilliseconds,
            fileId,
            source,
            offset);

    private void LogGetMiss(Stopwatch sw, string fileId)
        => logger.LogDebug(
            "TUS timing progressCache.Get +{RedisMs}ms fileId={FileId} hit=false",
            sw.ElapsedMilliseconds,
            fileId);

    private sealed class InMemoryProgressEntry
    {
        public InMemoryProgressEntry(long uploadLength)
        {
            UploadLength = uploadLength;
        }

        public object SyncRoot { get; } = new();

        public long UploadLength { get; set; }

        public long AcceptedOffset { get; set; }

        public long CommittedOffset { get; set; }

        public long NextBlockIndex { get; set; }

        public TusUploadProgressSnapshot ToSnapshot()
            => new(UploadLength, AcceptedOffset, CommittedOffset, NextBlockIndex);

        public static InMemoryProgressEntry FromSnapshot(TusUploadProgressSnapshot snapshot)
        {
            return new InMemoryProgressEntry(snapshot.UploadLength)
            {
                AcceptedOffset = snapshot.AcceptedOffset,
                CommittedOffset = snapshot.CommittedOffset,
                NextBlockIndex = snapshot.NextBlockIndex
            };
        }
    }
}
