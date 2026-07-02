using System.Collections.Concurrent;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using tusdotnet.Interfaces;
using tusdotnet.Models;

using Altinn.Broker.Core.Options;
using Xtensible.TusDotNet.Azure;

namespace Altinn.Broker.Integrations.Tus;

public class BrokerTusStore(
    ITusStorageResolver storageResolver,
    ITusExpirationDetailsStore expirationDetailsStore,
    IHttpContextAccessor httpContextAccessor,
    IOptions<AzureStorageOptions> azureStorageOptions) :
    ITusStore,
    ITusCreationStore,
    ITusReadableStore,
    ITusTerminationStore,
    ITusExpirationStore,
    ITusChecksumStore
{
    private readonly ConcurrentDictionary<string, long> _uploadLengths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, UploadState> _uploadStates = new(StringComparer.OrdinalIgnoreCase);

    public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
    {
        var store = await GetRequiredStore(fileId, cancellationToken);
        var state = await GetOrCreateUploadStateAsync(store, fileId, cancellationToken);

        using var chunkBuffer = new MemoryStream();
        await stream.CopyToAsync(chunkBuffer, cancellationToken);
        var chunk = chunkBuffer.ToArray();
        if (chunk.Length == 0)
        {
            return 0;
        }

        string blockId;
        long acceptedOffset;
        bool isFinalChunk;
        int chunkLength = chunk.Length;

        lock (state.SyncRoot)
        {
            if (state.Fault is not null)
            {
                throw new TusStoreException($"Buffered TUS upload failed for file id {fileId}. {state.Fault.Message}");
            }

            blockId = BuildBlockId(state.NextBlockIndex++);
            state.BlockIds.Add(blockId);
            state.AcceptedOffset += chunkLength;
            acceptedOffset = state.AcceptedOffset;
            isFinalChunk = acceptedOffset >= state.UploadLength;
            state.PendingUploads++;
            state.UploadMd5.TransformBlock(chunk, 0, chunkLength, null, 0);
        }

        _ = Task.Run(() => UploadBlockAsync(fileId, state, blockId, chunk));

        // For intermediate chunks we ACK as soon as bytes are read into memory.
        // For the final chunk we still wait until all buffered data is committed to storage.
        if (isFinalChunk)
        {
            await WaitForCommittedOffsetAsync(fileId, state, acceptedOffset, cancellationToken);
            await FinalizeUploadAsync(fileId, state, cancellationToken);
            CleanupUploadState(fileId);
        }

        return chunkLength;
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
        if (_uploadStates.TryGetValue(fileId, out var state))
        {
            lock (state.SyncRoot)
            {
                if (state.Fault is not null)
                {
                    throw new TusStoreException($"Buffered TUS upload failed for file id {fileId}. {state.Fault.Message}");
                }

                return state.AcceptedOffset;
            }
        }

        var store = await GetRequiredStore(fileId, cancellationToken);
        return await store.GetUploadOffsetAsync(fileId, cancellationToken);
    }

    public async Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
    {
        var fileTransferId = GetFileTransferIdFromRoute();
        var store = await GetRequiredStore(fileTransferId, cancellationToken);
        var fileId = await store.CreateFileAsync(uploadLength, metadata, cancellationToken);
        _uploadLengths[fileId] = uploadLength;
        _uploadStates[fileId] = new UploadState(uploadLength, initialOffset: 0, azureStorageOptions.Value.ConcurrentUploadThreads);
        return fileId;
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
        CleanupUploadState(fileId);

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

    private async Task<long> GetCachedUploadLengthAsync(
        AzureBlobTusStore store,
        string fileId,
        CancellationToken cancellationToken)
    {
        if (_uploadLengths.TryGetValue(fileId, out var cachedLength))
        {
            return cachedLength;
        }

        var uploadLength = await store.GetUploadLengthAsync(fileId, cancellationToken)
            ?? throw new TusStoreException($"Upload length is missing for file id {fileId}");
        _uploadLengths[fileId] = uploadLength;
        return uploadLength;
    }

    private async Task<UploadState> GetOrCreateUploadStateAsync(
        AzureBlobTusStore store,
        string fileId,
        CancellationToken cancellationToken)
    {
        if (_uploadStates.TryGetValue(fileId, out var existingState))
        {
            return existingState;
        }

        var uploadLength = await GetCachedUploadLengthAsync(store, fileId, cancellationToken);
        var currentOffset = await store.GetUploadOffsetAsync(fileId, cancellationToken);
        var newState = new UploadState(uploadLength, currentOffset, azureStorageOptions.Value.ConcurrentUploadThreads);
        return _uploadStates.GetOrAdd(fileId, newState);
    }

    private async Task UploadBlockAsync(string fileId, UploadState state, string blockId, byte[] chunk)
    {
        await state.ConcurrentUploader.WaitAsync();
        try
        {
            await storageResolver.StageTusBlockAsync(fileId, blockId, chunk, CancellationToken.None);
            lock (state.SyncRoot)
            {
                state.CommittedOffset += chunk.Length;
                state.PendingUploads--;
                var previousProgress = state.ProgressSignal;
                state.ProgressSignal = NewProgressSignal();
                previousProgress.TrySetResult(state.CommittedOffset);
            }
        }
        catch (Exception ex)
        {
            lock (state.SyncRoot)
            {
                state.Fault = ex;
                state.PendingUploads = Math.Max(state.PendingUploads - 1, 0);
                var previousProgress = state.ProgressSignal;
                state.ProgressSignal = NewProgressSignal();
                previousProgress.TrySetException(ex);
            }
        }
        finally
        {
            state.ConcurrentUploader.Release();
        }
    }

    private static TaskCompletionSource<long> NewProgressSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task WaitForCommittedOffsetAsync(
        string fileId,
        UploadState state,
        long targetOffset,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;
            lock (state.SyncRoot)
            {
                if (state.Fault is not null)
                {
                    throw new TusStoreException($"Buffered TUS upload failed for file id {fileId}. {state.Fault.Message}");
                }

                if (state.CommittedOffset >= targetOffset)
                {
                    return;
                }

                waitTask = state.ProgressSignal.Task;
            }

            await waitTask.WaitAsync(cancellationToken);
        }
    }

    private async Task FinalizeUploadAsync(string fileId, UploadState state, CancellationToken cancellationToken)
    {
        byte[] md5Hash;
        List<string> blockIds;

        lock (state.SyncRoot)
        {
            if (state.Fault is not null)
            {
                throw new TusStoreException($"Buffered TUS upload failed for file id {fileId}. {state.Fault.Message}");
            }

            if (state.PendingUploads > 0)
            {
                throw new TusStoreException($"Buffered TUS upload for file id {fileId} has {state.PendingUploads} pending block uploads.");
            }

            state.UploadMd5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            md5Hash = state.UploadMd5.Hash
                ?? throw new TusStoreException($"Failed to calculate MD5 for file id {fileId}.");
            blockIds = state.BlockIds.ToList();
        }

        if (blockIds.Count == 0)
        {
            throw new TusStoreException($"Cannot finalize TUS upload for file id {fileId} because no blocks were staged.");
        }

        await storageResolver.CommitTusBlocksAsync(fileId, blockIds, md5Hash, cancellationToken);
    }

    private void CleanupUploadState(string fileId)
    {
        if (_uploadStates.TryRemove(fileId, out var state))
        {
            state.UploadMd5.Dispose();
            state.ConcurrentUploader.Dispose();
        }

        _uploadLengths.TryRemove(fileId, out _);
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

    private static string BuildBlockId(long blockIndex)
    {
        var blockId = blockIndex.ToString("D12");
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(blockId));
    }

    private sealed class UploadState
    {
        public UploadState(long uploadLength, long initialOffset, int maxParallelBlockUploads)
        {
            UploadLength = uploadLength;
            AcceptedOffset = initialOffset;
            CommittedOffset = initialOffset;
            ProgressSignal = NewProgressSignal();
            ConcurrentUploader = new SemaphoreSlim(Math.Max(maxParallelBlockUploads, 1));
            UploadMd5 = MD5.Create();
        }

        public object SyncRoot { get; } = new();

        public List<string> BlockIds { get; } = new();

        public long UploadLength { get; }

        public long AcceptedOffset { get; set; }

        public long CommittedOffset { get; set; }

        public int PendingUploads { get; set; }

        public long NextBlockIndex { get; set; }

        public Exception? Fault { get; set; }

        public TaskCompletionSource<long> ProgressSignal { get; set; }

        public SemaphoreSlim ConcurrentUploader { get; }

        public MD5 UploadMd5 { get; }
    }
}
