using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;

namespace Altinn.Broker.Integrations.Tus;

public readonly record struct PartialUploadInfo(Guid FileTransferId, long UploadLength);

public enum TusConcatStatus
{
    Pending,
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

    Task ClearFinalConcatPartialReferencesAsync(string fileId, CancellationToken cancellationToken);

    Task RemovePartialAsync(string partialFileId, CancellationToken cancellationToken);

    Task RemoveUploadAsync(string fileId, CancellationToken cancellationToken);

    Task RemoveFinalConcatAsync(string fileId, CancellationToken cancellationToken);
}

public sealed class TusPartialUploadRegistry(IDistributedCache distributedCache) : ITusPartialUploadRegistry
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheExpiration
    };

    private static string PartialInfoKey(string partialFileId) => $"tus-partial-info:{NormalizeId(partialFileId)}";

    private static string UploadLengthKey(string fileId) => $"tus-upload-length:{NormalizeId(fileId)}";

    private static string FinalConcatKey(string fileId) => $"tus-final-concat:{NormalizeId(fileId)}";

    private static string ConcatStatusKey(string fileId) => $"tus-concat-status:{NormalizeId(fileId)}";

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
        => SetCachedValueAsync(ConcatStatusKey(fileId), TusConcatStatus.Complete.ToString(), cancellationToken);

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

    private sealed record PartialUploadInfoDto(Guid FileTransferId, long UploadLength);
}
