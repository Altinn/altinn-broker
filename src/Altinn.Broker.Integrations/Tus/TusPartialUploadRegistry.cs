using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;

using StackExchange.Redis;

namespace Altinn.Broker.Integrations.Tus;

public readonly record struct PartialUploadInfo(Guid FileTransferId, long UploadLength);

public enum TusConcatStatus
{
    Pending,
    InProgress,
    Complete
}

public interface ITusPartialUploadRegistry
{
    Task RegisterPartialAsync(string partialFileId, Guid fileTransferId, long uploadLength, CancellationToken cancellationToken);

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

    private static string NormalizeId(string fileId) => TusRouteHelper.NormalizePartialFileId(fileId);

    public async Task RegisterPartialAsync(
        string partialFileId,
        Guid fileTransferId,
        long uploadLength,
        CancellationToken cancellationToken)
    {
        await SetCachedValueAsync(
            PartialInfoKey(partialFileId),
            JsonSerializer.Serialize(new PartialUploadInfoDto(fileTransferId, uploadLength), JsonOptions),
            cancellationToken);
        await RegisterUploadAsync(partialFileId, uploadLength, cancellationToken);
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
        return dto is null ? null : new PartialUploadInfo(dto.FileTransferId, dto.UploadLength);
    }

    public async Task<long?> TryGetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        var value = await distributedCache.GetStringAsync(UploadLengthKey(fileId), cancellationToken);
        return long.TryParse(value, out var uploadLength) ? uploadLength : null;
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
        await SetCachedValueAsync(ConcatStatusKey(fileId), TusConcatStatus.Pending.ToString(), cancellationToken);
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

    private sealed record PartialUploadInfoDto(Guid FileTransferId, long UploadLength);
}
