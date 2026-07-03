using System.Collections.Concurrent;
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

    private readonly ConcurrentDictionary<string, PartialUploadInfo> _partials = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _uploadLengths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string[]> _finalConcats = new(StringComparer.OrdinalIgnoreCase);

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
        var info = new PartialUploadInfo(fileTransferId, uploadLength);
        _partials[partialFileId] = info;
        _uploadLengths[partialFileId] = uploadLength;

        var options = CreateCacheOptions();
        await cache.SetStringAsync(
            PartialKey(partialFileId),
            JsonSerializer.Serialize(info),
            options,
            cancellationToken);
        await cache.SetStringAsync(
            UploadLengthKey(partialFileId),
            uploadLength.ToString(),
            options,
            cancellationToken);
    }

    public async Task RegisterUploadAsync(string fileId, long uploadLength, CancellationToken cancellationToken = default)
    {
        _uploadLengths[fileId] = uploadLength;
        await cache.SetStringAsync(
            UploadLengthKey(fileId),
            uploadLength.ToString(),
            CreateCacheOptions(),
            cancellationToken);
    }

    public async Task<(Guid FileTransferId, long UploadLength)?> TryGetPartialInfoAsync(
        string partialFileId,
        CancellationToken cancellationToken = default)
    {
        if (_partials.TryGetValue(partialFileId, out var localInfo))
        {
            return (localInfo.FileTransferId, localInfo.UploadLength);
        }

        var json = await cache.GetStringAsync(PartialKey(partialFileId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var info = JsonSerializer.Deserialize<PartialUploadInfo>(json);
        if (info is null)
        {
            return null;
        }

        _partials[partialFileId] = info;
        _uploadLengths[partialFileId] = info.UploadLength;
        return (info.FileTransferId, info.UploadLength);
    }

    public async Task<long?> TryGetUploadLengthAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (_uploadLengths.TryGetValue(fileId, out var localLength))
        {
            return localLength;
        }

        var lengthValue = await cache.GetStringAsync(UploadLengthKey(fileId), cancellationToken);
        if (long.TryParse(lengthValue, out var uploadLength))
        {
            _uploadLengths[fileId] = uploadLength;
            return uploadLength;
        }

        var partialInfo = await TryGetPartialInfoAsync(fileId, cancellationToken);
        return partialInfo?.UploadLength;
    }

    public async Task<bool> IsKnownUploadAsync(string fileId, CancellationToken cancellationToken = default)
        => await TryGetUploadLengthAsync(fileId, cancellationToken) is not null;

    public async Task<bool> IsPartialAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (_partials.ContainsKey(fileId))
        {
            return true;
        }

        var json = await cache.GetStringAsync(PartialKey(fileId), cancellationToken);
        return !string.IsNullOrWhiteSpace(json);
    }

    public async Task<Guid?> TryGetFileTransferIdAsync(string tusFileId, CancellationToken cancellationToken = default)
    {
        if (_partials.TryGetValue(tusFileId, out var localInfo))
        {
            return localInfo.FileTransferId;
        }

        var partialInfo = await TryGetPartialInfoAsync(tusFileId, cancellationToken);
        return partialInfo?.FileTransferId;
    }

    public async Task RemovePartialAsync(string partialFileId, CancellationToken cancellationToken = default)
    {
        _partials.TryRemove(partialFileId, out _);
        _uploadLengths.TryRemove(partialFileId, out _);
        await cache.RemoveAsync(PartialKey(partialFileId), cancellationToken);
        await cache.RemoveAsync(UploadLengthKey(partialFileId), cancellationToken);
    }

    public async Task RemoveUploadAsync(string fileId, CancellationToken cancellationToken = default)
    {
        _uploadLengths.TryRemove(fileId, out _);
        await cache.RemoveAsync(UploadLengthKey(fileId), cancellationToken);
    }

    public async Task RegisterFinalConcatAsync(string finalFileId, string[] partialFileIds, CancellationToken cancellationToken = default)
    {
        _finalConcats[finalFileId] = partialFileIds;
        await cache.SetStringAsync(
            FinalConcatKey(finalFileId),
            JsonSerializer.Serialize(partialFileIds),
            CreateCacheOptions(),
            cancellationToken);
    }

    public async Task<string[]?> TryGetFinalConcatPartialIdsAsync(string finalFileId, CancellationToken cancellationToken = default)
    {
        if (_finalConcats.TryGetValue(finalFileId, out var localIds))
        {
            return localIds;
        }

        var json = await cache.GetStringAsync(FinalConcatKey(finalFileId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var partialIds = JsonSerializer.Deserialize<string[]>(json);
        if (partialIds is not null)
        {
            _finalConcats[finalFileId] = partialIds;
        }

        return partialIds;
    }

    public async Task RemoveFinalConcatAsync(string finalFileId, CancellationToken cancellationToken = default)
    {
        _finalConcats.TryRemove(finalFileId, out _);
        await cache.RemoveAsync(FinalConcatKey(finalFileId), cancellationToken);
    }

    private static DistributedCacheEntryOptions CreateCacheOptions()
        => new()
        {
            AbsoluteExpirationRelativeToNow = CacheExpiration
        };
}
