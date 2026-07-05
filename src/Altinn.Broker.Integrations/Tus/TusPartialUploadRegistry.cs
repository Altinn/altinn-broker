using System.Text.Json;

using Altinn.Broker.Application;

using Microsoft.Extensions.Caching.Hybrid;

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

public sealed class TusPartialUploadRegistry(HybridCache cache) : ITusPartialUploadRegistry
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = CacheExpiration,
        Flags = HybridCacheEntryFlags.DisableLocalCache
    };

    private static string PartialInfoKey(string partialFileId) => $"tus-partial-info:{NormalizeId(partialFileId)}";

    private static string UploadLengthKey(string fileId) => $"tus-upload-length:{NormalizeId(fileId)}";

    private static string FinalConcatKey(string fileId) => $"tus-final-concat:{NormalizeId(fileId)}";

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
        var json = await cache.GetOptionalStringAsync(PartialInfoKey(partialFileId), CacheOptions, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var dto = JsonSerializer.Deserialize<PartialUploadInfoDto>(json, JsonOptions);
        return dto is null ? null : new PartialUploadInfo(dto.FileTransferId, dto.UploadLength);
    }

    public async Task<long?> TryGetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        var value = await cache.GetOptionalStringAsync(UploadLengthKey(fileId), CacheOptions, cancellationToken);
        return long.TryParse(value, out var uploadLength) ? uploadLength : null;
    }

    public async Task<bool> IsKnownUploadAsync(string fileId, CancellationToken cancellationToken)
        => await TryGetUploadLengthAsync(fileId, cancellationToken) is not null;

    public async Task<bool> IsPartialAsync(string fileId, CancellationToken cancellationToken)
        => await TryGetPartialInfoAsync(fileId, cancellationToken) is not null;

    public async Task<Guid?> TryGetFileTransferIdAsync(string tusFileId, CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeId(tusFileId);
        var partialInfo = await TryGetPartialInfoAsync(normalizedId, cancellationToken);
        return partialInfo?.FileTransferId;
    }

    public Task RegisterFinalConcatAsync(
        string fileId,
        IReadOnlyList<string> partialFileIds,
        CancellationToken cancellationToken)
        => SetCachedValueAsync(FinalConcatKey(fileId), JsonSerializer.Serialize(partialFileIds, JsonOptions), cancellationToken);

    public async Task<string[]?> TryGetFinalConcatPartialIdsAsync(string fileId, CancellationToken cancellationToken)
    {
        var json = await cache.GetOptionalStringAsync(FinalConcatKey(fileId), CacheOptions, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<string[]>(json, JsonOptions);
    }

    public Task RemovePartialAsync(string partialFileId, CancellationToken cancellationToken)
        => Task.WhenAll(
            cache.RemoveKeyAsync(PartialInfoKey(partialFileId), cancellationToken),
            cache.RemoveKeyAsync(UploadLengthKey(partialFileId), cancellationToken));

    public Task RemoveUploadAsync(string fileId, CancellationToken cancellationToken)
        => cache.RemoveKeyAsync(UploadLengthKey(fileId), cancellationToken);

    public Task RemoveFinalConcatAsync(string fileId, CancellationToken cancellationToken)
        => cache.RemoveKeyAsync(FinalConcatKey(fileId), cancellationToken);

    private Task SetCachedValueAsync(string key, string value, CancellationToken cancellationToken)
        => cache.SetStringAsync(key, value, CacheOptions, cancellationToken);

    private sealed record PartialUploadInfoDto(Guid FileTransferId, long UploadLength);
}
