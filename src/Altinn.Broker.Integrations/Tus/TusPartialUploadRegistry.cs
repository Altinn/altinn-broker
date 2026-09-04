using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;

using StackExchange.Redis;

namespace Altinn.Broker.Integrations.Tus;

/// <summary>
/// <paramref name="BaseOffset"/> is where this partial's first byte sits in the assembled file.
/// </summary>
public readonly record struct PartialUploadInfo(Guid FileTransferId, long UploadLength, int PartialIndex, long BaseOffset);

public enum TusConcatStatus
{
    Pending,
    InProgress,
    Complete
}

public interface ITusPartialUploadRegistry
{
    Task RegisterPartialAsync(string partialFileId, Guid fileTransferId, long uploadLength, CancellationToken cancellationToken);

    /// <summary>
    /// Where the next partial would start, i.e. the total length of everything created so far.
    /// </summary>
    Task<long> PeekNextBaseOffsetAsync(Guid fileTransferId, CancellationToken cancellationToken);

    /// <summary>
    /// Blocks staged on one stripe, shared by every partial writing into it.
    /// </summary>
    Task<long> IncrementStripeBlockCountAsync(Guid fileTransferId, int stripeIndex, int delta, CancellationToken cancellationToken);

    Task ClearStripeBlockCountsAsync(Guid fileTransferId, CancellationToken cancellationToken);

    Task RegisterUploadAsync(string fileId, long uploadLength, CancellationToken cancellationToken);

    Task<PartialUploadInfo?> TryGetPartialInfoAsync(string partialFileId, CancellationToken cancellationToken);

    Task<long?> TryGetUploadLengthAsync(string fileId, CancellationToken cancellationToken);

    Task<bool> IsKnownUploadAsync(string fileId, CancellationToken cancellationToken);

    Task<bool> IsPartialAsync(string fileId, CancellationToken cancellationToken);

    Task<Guid?> TryGetFileTransferIdAsync(string tusFileId, CancellationToken cancellationToken);

    Task RegisterFinalConcatAsync(string fileId, IReadOnlyList<string> partialFileIds, CancellationToken cancellationToken);

    Task<string[]?> TryGetFinalConcatPartialIdsAsync(string fileId, CancellationToken cancellationToken);

    Task<TusConcatStatus?> TryGetConcatStatusAsync(string fileId, CancellationToken cancellationToken);

    Task MarkConcatCompleteAsync(string fileId, CancellationToken cancellationToken);

    Task MarkConcatInProgressAsync(string fileId, CancellationToken cancellationToken);

    Task<bool> TryAcquireConcatEnqueueSlotAsync(string fileId, CancellationToken cancellationToken);

    Task<bool> TryBeginConcatJobAsync(string fileId, CancellationToken cancellationToken);

    Task ReleaseConcatRunningLockAsync(string fileId, CancellationToken cancellationToken);

    Task<bool> TryAcquirePublishEnqueueSlotAsync(string fileId, CancellationToken cancellationToken);

    Task<bool> IsConcatRunningAsync(string fileId, CancellationToken cancellationToken);

    Task ClearConcatEnqueueSlotAsync(string fileId, CancellationToken cancellationToken);

    Task ClearPublishEnqueueSlotAsync(string fileId, CancellationToken cancellationToken);

    Task ClearFinalConcatPartialReferencesAsync(string fileId, CancellationToken cancellationToken);

    Task RemovePartialAsync(string partialFileId, CancellationToken cancellationToken);

    Task RemoveUploadAsync(string fileId, CancellationToken cancellationToken);

    Task RemoveFinalConcatAsync(string fileId, CancellationToken cancellationToken);
}

public sealed class TusPartialUploadRegistry(
    IDistributedCache distributedCache,
    IConnectionMultiplexer? redis = null) : ITusPartialUploadRegistry
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    private static readonly TimeSpan RunningLockExpiration = TimeSpan.FromHours(8);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheExpiration
    };

    private readonly IDatabase? _database = redis?.GetDatabase();

    private static string PartialInfoKey(string partialFileId) => $"tus-partial-info:{NormalizeId(partialFileId)}";

    private static string UploadLengthKey(string fileId) => $"tus-upload-length:{NormalizeId(fileId)}";

    private static string FinalConcatKey(string fileId) => $"tus-final-concat:{NormalizeId(fileId)}";

    private static string ConcatStatusKey(string fileId) => $"tus-concat-status:{NormalizeId(fileId)}";

    private static string ConcatEnqueueKey(string fileId) => $"tus-concat-enqueued:{NormalizeId(fileId)}";

    private static string ConcatRunningKey(string fileId) => $"tus-concat-running:{NormalizeId(fileId)}";

    private static string PublishEnqueueKey(string fileId) => $"tus-publish-enqueued:{NormalizeId(fileId)}";

    private static string PartialSlotKey(Guid fileTransferId) => $"tus-partial-slots:{fileTransferId:N}";

    private static string StripeBlockCountKey(Guid fileTransferId) => $"tus-stripe-blocks:{fileTransferId:N}";

    private const string NextIndexField = "nextIndex";
    private const string NextOffsetField = "nextOffset";

    // Index and offset must be handed out together: two separate increments can interleave and leave a
    // partial with the lower index sitting at the higher offset.
    private const string AllocatePartialSlotScript = """
        local uploadLength = tonumber(ARGV[1])
        local ttl = tonumber(ARGV[2])

        local index = redis.call('HINCRBY', KEYS[1], 'nextIndex', 1) - 1
        local nextOffset = redis.call('HINCRBY', KEYS[1], 'nextOffset', uploadLength)
        redis.call('EXPIRE', KEYS[1], ttl)
        return {index, nextOffset - uploadLength}
        """;

    private static string NormalizeId(string fileId) => TusRouteHelper.NormalizePartialFileId(fileId);

    public async Task RegisterPartialAsync(
        string partialFileId,
        Guid fileTransferId,
        long uploadLength,
        CancellationToken cancellationToken)
    {
        var existing = await TryGetPartialInfoAsync(partialFileId, cancellationToken);
        var (partialIndex, baseOffset) = existing is { } known
            ? (known.PartialIndex, known.BaseOffset)
            : await AllocatePartialSlotAsync(fileTransferId, uploadLength, cancellationToken);
        await SetCachedValueAsync(
            PartialInfoKey(partialFileId),
            JsonSerializer.Serialize(new PartialUploadInfoDto(fileTransferId, uploadLength, partialIndex, baseOffset), JsonOptions),
            cancellationToken);
        await RegisterUploadAsync(partialFileId, uploadLength, cancellationToken);
    }

    public async Task<long> PeekNextBaseOffsetAsync(Guid fileTransferId, CancellationToken cancellationToken)
    {
        if (_database is not null)
        {
            var value = await _database.HashGetAsync(PartialSlotKey(fileTransferId), NextOffsetField);
            return value.HasValue ? (long)value : 0;
        }

        var cached = await distributedCache.GetStringAsync(PartialSlotKey(fileTransferId) + ":offset", cancellationToken);
        return long.TryParse(cached, out var offset) ? offset : 0;
    }

    public async Task<long> IncrementStripeBlockCountAsync(
        Guid fileTransferId,
        int stripeIndex,
        int delta,
        CancellationToken cancellationToken)
    {
        var key = StripeBlockCountKey(fileTransferId);
        if (_database is not null)
        {
            var count = await _database.HashIncrementAsync(key, stripeIndex, delta);
            await _database.KeyExpireAsync(key, CacheExpiration);
            return count;
        }

        var json = await distributedCache.GetStringAsync(key, cancellationToken);
        var counts = string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<Dictionary<int, long>>(json, JsonOptions) ?? [];
        counts.TryGetValue(stripeIndex, out var current);
        var next = current + delta;
        counts[stripeIndex] = next;
        await SetCachedValueAsync(key, JsonSerializer.Serialize(counts, JsonOptions), cancellationToken);
        return next;
    }

    public Task ClearStripeBlockCountsAsync(Guid fileTransferId, CancellationToken cancellationToken)
    {
        if (_database is not null)
        {
            return _database.KeyDeleteAsync(StripeBlockCountKey(fileTransferId));
        }

        return distributedCache.RemoveAsync(StripeBlockCountKey(fileTransferId), cancellationToken);
    }

    public Task RegisterUploadAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
        => SetCachedValueAsync(UploadLengthKey(fileId), uploadLength.ToString(), cancellationToken);

    public async Task<PartialUploadInfo?> TryGetPartialInfoAsync(string partialFileId, CancellationToken cancellationToken)
    {
        var json = await distributedCache.GetStringAsync(PartialInfoKey(partialFileId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var dto = JsonSerializer.Deserialize<PartialUploadInfoDto>(json, JsonOptions);
        return dto is null ? null : new PartialUploadInfo(dto.FileTransferId, dto.UploadLength, dto.PartialIndex, dto.BaseOffset);
    }

    public async Task<long?> TryGetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        var value = await distributedCache.GetStringAsync(UploadLengthKey(fileId), cancellationToken);
        if (long.TryParse(value, out var uploadLength))
        {
            return uploadLength;
        }
        return (await TryGetPartialInfoAsync(fileId, cancellationToken))?.UploadLength;
    }

    public async Task<bool> IsKnownUploadAsync(string fileId, CancellationToken cancellationToken)
        => await TryGetUploadLengthAsync(fileId, cancellationToken) is not null;

    public async Task<bool> IsPartialAsync(string fileId, CancellationToken cancellationToken)
        => await TryGetPartialInfoAsync(fileId, cancellationToken) is not null;

    public async Task<Guid?> TryGetFileTransferIdAsync(string tusFileId, CancellationToken cancellationToken)
    {
        var partialInfo = await TryGetPartialInfoAsync(tusFileId, cancellationToken);
        return partialInfo?.FileTransferId;
    }

    public async Task RegisterFinalConcatAsync(
        string fileId,
        IReadOnlyList<string> partialFileIds,
        CancellationToken cancellationToken)
    {
        await ClearConcatEnqueueSlotAsync(fileId, cancellationToken);
        await ReleaseConcatRunningLockAsync(fileId, cancellationToken);
        await ClearPublishEnqueueSlotAsync(fileId, cancellationToken);
        await SetCachedValueAsync(FinalConcatKey(fileId), JsonSerializer.Serialize(partialFileIds, JsonOptions), cancellationToken);
        if (await TryGetConcatStatusAsync(fileId, cancellationToken) is null)
        {
            await SetCachedValueAsync(ConcatStatusKey(fileId), TusConcatStatus.Pending.ToString(), cancellationToken);
        }
    }

    public async Task<string[]?> TryGetFinalConcatPartialIdsAsync(string fileId, CancellationToken cancellationToken)
    {
        var json = await distributedCache.GetStringAsync(FinalConcatKey(fileId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<string[]>(json, JsonOptions);
    }

    public async Task<TusConcatStatus?> TryGetConcatStatusAsync(string fileId, CancellationToken cancellationToken)
    {
        var value = await distributedCache.GetStringAsync(ConcatStatusKey(fileId), cancellationToken);
        return Enum.TryParse<TusConcatStatus>(value, ignoreCase: true, out var status) ? status : null;
    }

    public Task MarkConcatCompleteAsync(string fileId, CancellationToken cancellationToken)
        => Task.WhenAll(
            SetCachedValueAsync(ConcatStatusKey(fileId), TusConcatStatus.Complete.ToString(), cancellationToken),
            ClearConcatEnqueueSlotAsync(fileId, cancellationToken));

    public Task MarkConcatInProgressAsync(string fileId, CancellationToken cancellationToken)
        => SetCachedValueAsync(ConcatStatusKey(fileId), TusConcatStatus.InProgress.ToString(), cancellationToken);

    public async Task<bool> TryAcquireConcatEnqueueSlotAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = NormalizeId(fileId);
        var status = await TryGetConcatStatusAsync(fileId, cancellationToken);
        if (status == TusConcatStatus.Complete)
        {
            return false;
        }

        if (status == TusConcatStatus.InProgress && await IsConcatRunningAsync(fileId, cancellationToken))
        {
            return false;
        }

        return await TrySetOnceAsync(ConcatEnqueueKey(fileId), "1", cancellationToken);
    }

    public async Task<bool> TryBeginConcatJobAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = NormalizeId(fileId);
        var status = await TryGetConcatStatusAsync(fileId, cancellationToken);
        if (status == TusConcatStatus.Complete)
        {
            return false;
        }

        if (!await TrySetOnceAsync(ConcatRunningKey(fileId), "1", cancellationToken, RunningLockExpiration))
        {
            return false;
        }

        if (status == TusConcatStatus.Pending)
        {
            await MarkConcatInProgressAsync(fileId, cancellationToken);
        }

        return true;
    }

    public Task ReleaseConcatRunningLockAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = NormalizeId(fileId);
        if (_database is not null)
        {
            return _database.KeyDeleteAsync(ConcatRunningKey(fileId));
        }

        return distributedCache.RemoveAsync(ConcatRunningKey(fileId), cancellationToken);
    }

    public Task<bool> TryAcquirePublishEnqueueSlotAsync(string fileId, CancellationToken cancellationToken)
        => TrySetOnceAsync(PublishEnqueueKey(NormalizeId(fileId)), "1", cancellationToken);

    public async Task<bool> IsConcatRunningAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = NormalizeId(fileId);
        if (_database is not null)
        {
            return await _database.KeyExistsAsync(ConcatRunningKey(fileId));
        }

        return !string.IsNullOrWhiteSpace(
            await distributedCache.GetStringAsync(ConcatRunningKey(fileId), cancellationToken));
    }

    public Task ClearConcatEnqueueSlotAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = NormalizeId(fileId);
        if (_database is not null)
        {
            return _database.KeyDeleteAsync(ConcatEnqueueKey(fileId));
        }

        return distributedCache.RemoveAsync(ConcatEnqueueKey(fileId), cancellationToken);
    }

    public Task ClearPublishEnqueueSlotAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = NormalizeId(fileId);
        if (_database is not null)
        {
            return _database.KeyDeleteAsync(PublishEnqueueKey(fileId));
        }

        return distributedCache.RemoveAsync(PublishEnqueueKey(fileId), cancellationToken);
    }

    public Task ClearFinalConcatPartialReferencesAsync(string fileId, CancellationToken cancellationToken)
        => distributedCache.RemoveAsync(FinalConcatKey(fileId), cancellationToken);

    public Task RemovePartialAsync(string partialFileId, CancellationToken cancellationToken)
        => Task.WhenAll(
            distributedCache.RemoveAsync(PartialInfoKey(partialFileId), cancellationToken),
            distributedCache.RemoveAsync(UploadLengthKey(partialFileId), cancellationToken));

    public Task RemoveUploadAsync(string fileId, CancellationToken cancellationToken)
        => distributedCache.RemoveAsync(UploadLengthKey(fileId), cancellationToken);

    public Task RemoveFinalConcatAsync(string fileId, CancellationToken cancellationToken)
        => Task.WhenAll(
            distributedCache.RemoveAsync(FinalConcatKey(fileId), cancellationToken),
            distributedCache.RemoveAsync(ConcatStatusKey(fileId), cancellationToken));

    private Task SetCachedValueAsync(string key, string value, CancellationToken cancellationToken)
        => distributedCache.SetStringAsync(key, value, CacheOptions, cancellationToken);

    private async Task<bool> TrySetOnceAsync(
        string key,
        string value,
        CancellationToken cancellationToken,
        TimeSpan? expiration = null)
    {
        if (_database is not null)
        {
            return await _database.StringSetAsync(
                key,
                value,
                expiration ?? CacheExpiration,
                When.NotExists);
        }

        var existing = await distributedCache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return false;
        }

        await distributedCache.SetStringAsync(
            key,
            value,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? CacheExpiration
            },
            cancellationToken);
        return true;
    }

    private async Task<(int PartialIndex, long BaseOffset)> AllocatePartialSlotAsync(
        Guid fileTransferId,
        long uploadLength,
        CancellationToken cancellationToken)
    {
        var key = PartialSlotKey(fileTransferId);
        if (_database is not null)
        {
            var result = (RedisResult[]?)await _database.ScriptEvaluateAsync(
                AllocatePartialSlotScript,
                [key],
                [uploadLength, (int)CacheExpiration.TotalSeconds]);
            if (result is { Length: 2 })
            {
                return ((int)result[0], (long)result[1]);
            }
        }

        // Read-modify-write fallback for configurations without Redis, such as the test host. It carries
        // the same race as the rest of the non-Redis paths in this class and is not used in deployment.
        var indexKey = key + ":index";
        var offsetKey = key + ":offset";
        var currentIndex = await distributedCache.GetStringAsync(indexKey, cancellationToken);
        var currentOffset = await distributedCache.GetStringAsync(offsetKey, cancellationToken);
        var partialIndex = int.TryParse(currentIndex, out var parsedIndex) ? parsedIndex : 0;
        var baseOffset = long.TryParse(currentOffset, out var parsedOffset) ? parsedOffset : 0;
        await SetCachedValueAsync(indexKey, (partialIndex + 1).ToString(), cancellationToken);
        await SetCachedValueAsync(offsetKey, (baseOffset + uploadLength).ToString(), cancellationToken);
        return (partialIndex, baseOffset);
    }

    private sealed record PartialUploadInfoDto(Guid FileTransferId, long UploadLength, int PartialIndex = 0, long BaseOffset = 0);
}
