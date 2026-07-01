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
    ITusExpirationStore
{
    public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
    {
        var store = await GetRequiredStore(fileId, cancellationToken);
        return await store.AppendDataAsync(fileId, stream, cancellationToken);
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
        => Task.FromResult(Enumerable.Empty<string>());

    public Task<bool> VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken)
        => Task.FromResult(false);

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
