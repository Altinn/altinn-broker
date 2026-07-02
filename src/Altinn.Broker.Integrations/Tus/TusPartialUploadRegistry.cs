using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;

namespace Altinn.Broker.Integrations.Tus;

public interface ITusPartialUploadRegistry
{
    Task RegisterPartialAsync(string partialFileId, Guid fileTransferId, long uploadLength, CancellationToken cancellationToken = default);

    Task RegisterUploadAsync(string fileId, long uploadLength, CancellationToken cancellationToken = default);

    Task<(Guid FileTransferId, long UploadLength)?> TryGetPartialInfoAsync(string partialFileId, CancellationToken cancellationToken = default);

    Task<long?> TryGetUploadLengthAsync(string fileId, CancellationToken cancellationToken = default);

    Task<bool> IsKnownUploadAsync(string fileId, CancellationToken cancellationToken = default);

    Task<bool> IsPartialAsync(string fileId, CancellationToken cancellationToken = default);

    Task<Guid?> TryGetFileTransferIdAsync(string tusFileId, CancellationToken cancellationToken = default);

    Task RemovePartialAsync(string partialFileId, CancellationToken cancellationToken = default);

    Task RemoveUploadAsync(string fileId, CancellationToken cancellationToken = default);

    Task RegisterFinalConcatAsync(string finalFileId, string[] partialFileIds, CancellationToken cancellationToken = default);

    Task<string[]?> TryGetFinalConcatPartialIdsAsync(string finalFileId, CancellationToken cancellationToken = default);

    Task RemoveFinalConcatAsync(string finalFileId, CancellationToken cancellationToken = default);
}

public sealed class TusPartialUploadRegistry(IDistributedCache cache) : ITusPartialUploadRegistry
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    private static string PartialKey(string fileId) => $"tus-partial:{fileId}";

    private static string UploadLengthKey(string fileId) => $"tus-upload-length:{fileId}";

    private static string FinalConcatKey(string fileId) => $"tus-final-concat:{fileId}";

    private sealed record PartialUploadInfo(Guid FileTransferId, long UploadLength);

    public async Task RegisterPartialAsync(
        string partialFileId,
        Guid fileTransferId,
        long uploadLength,
        CancellationToken cancellationToken = default)
    {
        var options = CreateCacheOptions();
        await cache.SetStringAsync(
            PartialKey(partialFileId),
            JsonSerializer.Serialize(new PartialUploadInfo(fileTransferId, uploadLength)),
            options,
            cancellationToken);
        await cache.SetStringAsync(
            UploadLengthKey(partialFileId),
            uploadLength.ToString(),
            options,
            cancellationToken);
    }

    public Task RegisterUploadAsync(string fileId, long uploadLength, CancellationToken cancellationToken = default)
        => cache.SetStringAsync(
            UploadLengthKey(fileId),
            uploadLength.ToString(),
            CreateCacheOptions(),
            cancellationToken);

    public async Task<(Guid FileTransferId, long UploadLength)?> TryGetPartialInfoAsync(
        string partialFileId,
        CancellationToken cancellationToken = default)
    {
        var json = await cache.GetStringAsync(PartialKey(partialFileId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var info = JsonSerializer.Deserialize<PartialUploadInfo>(json);
        return info is null ? null : (info.FileTransferId, info.UploadLength);
    }

    public async Task<long?> TryGetUploadLengthAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var lengthValue = await cache.GetStringAsync(UploadLengthKey(fileId), cancellationToken);
        if (long.TryParse(lengthValue, out var uploadLength))
        {
            return uploadLength;
        }

        var partialInfo = await TryGetPartialInfoAsync(fileId, cancellationToken);
        return partialInfo?.UploadLength;
    }

    public async Task<bool> IsKnownUploadAsync(string fileId, CancellationToken cancellationToken = default)
        => await TryGetUploadLengthAsync(fileId, cancellationToken) is not null;

    public async Task<bool> IsPartialAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var json = await cache.GetStringAsync(PartialKey(fileId), cancellationToken);
        return !string.IsNullOrWhiteSpace(json);
    }

    public async Task<Guid?> TryGetFileTransferIdAsync(string tusFileId, CancellationToken cancellationToken = default)
    {
        var partialInfo = await TryGetPartialInfoAsync(tusFileId, cancellationToken);
        return partialInfo?.FileTransferId;
    }

    public async Task RemovePartialAsync(string partialFileId, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(PartialKey(partialFileId), cancellationToken);
        await cache.RemoveAsync(UploadLengthKey(partialFileId), cancellationToken);
    }

    public Task RemoveUploadAsync(string fileId, CancellationToken cancellationToken = default)
        => cache.RemoveAsync(UploadLengthKey(fileId), cancellationToken);

    public Task RegisterFinalConcatAsync(string finalFileId, string[] partialFileIds, CancellationToken cancellationToken = default)
        => cache.SetStringAsync(
            FinalConcatKey(finalFileId),
            JsonSerializer.Serialize(partialFileIds),
            CreateCacheOptions(),
            cancellationToken);

    public async Task<string[]?> TryGetFinalConcatPartialIdsAsync(string finalFileId, CancellationToken cancellationToken = default)
    {
        var json = await cache.GetStringAsync(FinalConcatKey(finalFileId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<string[]>(json);
    }

    public Task RemoveFinalConcatAsync(string finalFileId, CancellationToken cancellationToken = default)
        => cache.RemoveAsync(FinalConcatKey(finalFileId), cancellationToken);

    private static DistributedCacheEntryOptions CreateCacheOptions()
        => new()
        {
            AbsoluteExpirationRelativeToNow = CacheExpiration
        };
}
