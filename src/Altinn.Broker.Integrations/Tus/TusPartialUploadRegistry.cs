using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;

namespace Altinn.Broker.Integrations.Tus;

public readonly record struct PartialUploadInfo(Guid FileTransferId, long UploadLength);

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

    Task RemovePartialAsync(string partialFileId, CancellationToken cancellationToken);

    Task RemoveUploadAsync(string fileId, CancellationToken cancellationToken);

    Task RemoveFinalConcatAsync(string fileId, CancellationToken cancellationToken);
}

public sealed class TusPartialUploadRegistry(IDistributedCache cache) : ITusPartialUploadRegistry
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    private static string PartialInfoKey(string partialFileId) => $"tus-partial-info:{partialFileId}";

    private static string UploadLengthKey(string fileId) => $"tus-upload-length:{fileId}";

    private static string FinalConcatKey(string fileId) => $"tus-final-concat:{fileId}";

    public async Task RegisterPartialAsync(
        string partialFileId,
        Guid fileTransferId,
        long uploadLength,
        CancellationToken cancellationToken)
    {
        await SetCachedValueAsync(
            PartialInfoKey(partialFileId),
            JsonSerializer.Serialize(new PartialUploadInfoDto(fileTransferId, uploadLength)),
            cancellationToken);
        await RegisterUploadAsync(partialFileId, uploadLength, cancellationToken);
    }

    public Task RegisterUploadAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
        => SetCachedValueAsync(UploadLengthKey(fileId), uploadLength.ToString(), cancellationToken);

    public async Task<PartialUploadInfo?> TryGetPartialInfoAsync(string partialFileId, CancellationToken cancellationToken)
    {
        var json = await cache.GetStringAsync(PartialInfoKey(partialFileId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var dto = JsonSerializer.Deserialize<PartialUploadInfoDto>(json);
        return dto is null ? null : new PartialUploadInfo(dto.FileTransferId, dto.UploadLength);
    }

    public async Task<long?> TryGetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        var value = await cache.GetStringAsync(UploadLengthKey(fileId), cancellationToken);
        return long.TryParse(value, out var uploadLength) ? uploadLength : null;
    }

    public async Task<bool> IsKnownUploadAsync(string fileId, CancellationToken cancellationToken)
        => await TryGetUploadLengthAsync(fileId, cancellationToken) is not null;

    public async Task<bool> IsPartialAsync(string fileId, CancellationToken cancellationToken)
        => await TryGetPartialInfoAsync(fileId, cancellationToken) is not null;

    public async Task<Guid?> TryGetFileTransferIdAsync(string tusFileId, CancellationToken cancellationToken)
    {
        var partialInfo = await TryGetPartialInfoAsync(tusFileId, cancellationToken);
        if (partialInfo is not null)
        {
            return partialInfo.Value.FileTransferId;
        }

        if (Guid.TryParse(tusFileId, out var fileTransferId))
        {
            return fileTransferId;
        }

        return null;
    }

    public Task RegisterFinalConcatAsync(
        string fileId,
        IReadOnlyList<string> partialFileIds,
        CancellationToken cancellationToken)
        => SetCachedValueAsync(FinalConcatKey(fileId), JsonSerializer.Serialize(partialFileIds), cancellationToken);

    public async Task<string[]?> TryGetFinalConcatPartialIdsAsync(string fileId, CancellationToken cancellationToken)
    {
        var json = await cache.GetStringAsync(FinalConcatKey(fileId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<string[]>(json);
    }

    public Task RemovePartialAsync(string partialFileId, CancellationToken cancellationToken)
        => Task.WhenAll(
            cache.RemoveAsync(PartialInfoKey(partialFileId), cancellationToken),
            cache.RemoveAsync(UploadLengthKey(partialFileId), cancellationToken));

    public Task RemoveUploadAsync(string fileId, CancellationToken cancellationToken)
        => cache.RemoveAsync(UploadLengthKey(fileId), cancellationToken);

    public Task RemoveFinalConcatAsync(string fileId, CancellationToken cancellationToken)
        => cache.RemoveAsync(FinalConcatKey(fileId), cancellationToken);

    private Task SetCachedValueAsync(string key, string value, CancellationToken cancellationToken)
        => cache.SetStringAsync(
            key,
            value,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration
            },
            cancellationToken);

    private sealed record PartialUploadInfoDto(Guid FileTransferId, long UploadLength);
}
