using System.Collections.Concurrent;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Http;

using tusdotnet.Interfaces;
using tusdotnet.Models;

using Xtensible.TusDotNet.Azure;

namespace Altinn.Broker.Integrations.Tus;

public class BrokerTusStore(
    ITusStorageResolver storageResolver,
    ITusExpirationDetailsStore expirationDetailsStore,
    IHttpContextAccessor httpContextAccessor) :
    ITusStore,
    ITusCreationStore,
    ITusReadableStore,
    ITusTerminationStore,
    ITusExpirationStore,
    ITusChecksumStore
{
    private readonly ConcurrentDictionary<string, MD5> _uploadMd5Hashers = new(StringComparer.OrdinalIgnoreCase);

    public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
    {
        var store = await GetRequiredStore(fileId, cancellationToken);
        var md5 = await GetOrCreateUploadMd5Async(store, fileId, cancellationToken);

        using var chunkData = new MemoryStream();
        await stream.CopyToAsync(chunkData, cancellationToken);
        var chunkBytes = chunkData.ToArray();
        if (chunkBytes.Length > 0)
        {
            md5.TransformBlock(chunkBytes, 0, chunkBytes.Length, null, 0);
        }

        await using var chunkStream = new MemoryStream(chunkBytes, writable: false);
        var bytesWritten = await store.AppendDataAsync(fileId, chunkStream, cancellationToken);

        var uploadLength = await store.GetUploadLengthAsync(fileId, cancellationToken);
        var offset = await store.GetUploadOffsetAsync(fileId, cancellationToken);
        if (uploadLength is not null && offset >= uploadLength.Value)
        {
            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            if (md5.Hash is not null)
            {
                await storageResolver.SetStagingBlobMd5ChecksumAsync(fileId, md5.Hash, cancellationToken);
            }

            if (_uploadMd5Hashers.TryRemove(fileId, out var hasher))
            {
                hasher.Dispose();
            }
        }

        return bytesWritten;
    }

    public async Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken)
    {
        var store = await storageResolver.GetStoreForFileAsync(fileId, cancellationToken);
        return store is not null && await store.FileExistAsync(fileId, cancellationToken);
    }

    public async Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        var store = await GetRequiredStore(fileId, cancellationToken);
        return await store.GetUploadLengthAsync(fileId, cancellationToken);
    }

    public async Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
    {
        var store = await GetRequiredStore(fileId, cancellationToken);
        return await store.GetUploadOffsetAsync(fileId, cancellationToken);
    }

    public async Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
    {
        var fileTransferId = GetFileTransferIdFromRoute();
        var store = await GetRequiredStore(fileTransferId, cancellationToken);
        return await store.CreateFileAsync(uploadLength, metadata, cancellationToken);
    }

    public async Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken)
    {
        var store = await GetRequiredStore(fileId, cancellationToken);
        return await store.GetUploadMetadataAsync(fileId, cancellationToken);
    }

    public async Task<ITusFile> GetFileAsync(string fileId, CancellationToken cancellationToken)
    {
        var store = await GetRequiredStore(fileId, cancellationToken);
        return await store.GetFileAsync(fileId, cancellationToken);
    }

    public async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
    {
        var store = await GetRequiredStore(fileId, cancellationToken);
        await store.DeleteFileAsync(fileId, cancellationToken);
        if (_uploadMd5Hashers.TryRemove(fileId, out var hasher))
        {
            hasher.Dispose();
        }

        if (expirationDetailsStore is RedisTusExpirationDetailsStore redisExpirationStore)
        {
            await redisExpirationStore.RemoveExpirationAsync(fileId, cancellationToken);
        }
    }

    public Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken cancellationToken)
        => expirationDetailsStore.SetExpirationAsync(fileId, expires, cancellationToken);

    public Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken cancellationToken)
        => expirationDetailsStore.GetExpirationAsync(fileId, cancellationToken);

    public Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken)
        => expirationDetailsStore.GetExpiredFilesAsync(cancellationToken);

    public async Task<int> RemoveExpiredFilesAsync(CancellationToken cancellationToken)
    {
        var expiredFiles = await expirationDetailsStore.GetExpiredFilesAsync(cancellationToken);
        var removed = 0;
        foreach (var fileId in expiredFiles)
        {
            try
            {
                if (await FileExistAsync(fileId, cancellationToken))
                {
                    await DeleteFileAsync(fileId, cancellationToken);
                    removed++;
                }
            }
            catch (TusStoreException)
            {
                // File may already have been removed.
            }
        }

        return removed;
    }

    public Task<IEnumerable<string>> GetSupportedAlgorithmsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IEnumerable<string>>(new[] { "md5" });

    public async Task<bool> VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken)
    {
        var store = await GetRequiredStore(fileId, cancellationToken);
        return await store.VerifyChecksumAsync(fileId, algorithm, checksum, cancellationToken);
    }

    private async Task<MD5> GetOrCreateUploadMd5Async(AzureBlobTusStore store, string fileId, CancellationToken cancellationToken)
    {
        if (_uploadMd5Hashers.TryGetValue(fileId, out var existing))
        {
            return existing;
        }

        var md5 = MD5.Create();
        var offset = await store.GetUploadOffsetAsync(fileId, cancellationToken);
        if (offset > 0)
        {
            var file = await store.GetFileAsync(fileId, cancellationToken);
            await using var content = await file.GetContentAsync(cancellationToken);
            var buffer = new byte[1024 * 1024];
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                md5.TransformBlock(buffer, 0, read, null, 0);
            }
        }

        if (!_uploadMd5Hashers.TryAdd(fileId, md5))
        {
            md5.Dispose();
            return _uploadMd5Hashers[fileId];
        }

        return md5;
    }

    private async Task<AzureBlobTusStore> GetRequiredStore(string fileId, CancellationToken cancellationToken)
    {
        var store = await storageResolver.GetStoreForFileAsync(fileId, cancellationToken);
        if (store is null)
        {
            throw new TusStoreException($"No TUS store found for file id {fileId}");
        }

        return store;
    }

    private string GetFileTransferIdFromRoute()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new TusStoreException("Missing HTTP context");

        if (!TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var fileTransferId))
        {
            throw new TusStoreException("Missing file transfer id in route");
        }

        return fileTransferId.ToString();
    }
}
