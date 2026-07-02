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

        var shouldStartProcessor = false;
        long acceptedOffset;
        bool isFinalChunk;

        lock (state.SyncRoot)
        {
            if (state.Fault is not null)
            {
                throw new TusStoreException($"Buffered TUS upload failed for file id {fileId}. {state.Fault.Message}");
            }

            state.PendingChunks.Enqueue(chunk);
            state.AcceptedOffset += chunk.Length;
            acceptedOffset = state.AcceptedOffset;
            isFinalChunk = acceptedOffset >= state.UploadLength;

            if (!state.IsProcessing)
            {
                state.IsProcessing = true;
                shouldStartProcessor = true;
            }
        }

        if (shouldStartProcessor)
        {
            _ = Task.Run(() => ProcessPendingChunksAsync(fileId, store, state));
        }

        // For intermediate chunks we ACK as soon as bytes are read into memory.
        // For the final chunk we still wait until all buffered data is committed to storage.
        if (isFinalChunk)
        {
            await WaitForCommittedOffsetAsync(fileId, state, acceptedOffset, cancellationToken);
            await FinalizeUploadChecksumAsync(fileId, state, cancellationToken);
            CleanupUploadState(fileId);
        }

        return chunk.Length;
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
        _uploadStates[fileId] = new UploadState(uploadLength, initialOffset: 0);
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
        var newState = new UploadState(uploadLength, currentOffset);
        return _uploadStates.GetOrAdd(fileId, newState);
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

    private async Task ProcessPendingChunksAsync(string fileId, AzureBlobTusStore store, UploadState state)
    {
        while (true)
        {
            byte[]? chunk;
            lock (state.SyncRoot)
            {
                if (state.PendingChunks.Count == 0)
                {
                    state.IsProcessing = false;
                    return;
                }

                chunk = state.PendingChunks.Dequeue();
            }

            try
            {
                await using var chunkStream = new MemoryStream(chunk, writable: false);
                var bytesWritten = await store.AppendDataAsync(fileId, chunkStream, CancellationToken.None);
                if (bytesWritten != chunk.Length)
                {
                    throw new TusStoreException($"Unexpected append result for file id {fileId}. Expected {chunk.Length} bytes, wrote {bytesWritten} bytes.");
                }

                var md5 = await GetOrCreateUploadMd5Async(store, fileId, CancellationToken.None);
                md5.TransformBlock(chunk, 0, chunk.Length, null, 0);

                lock (state.SyncRoot)
                {
                    state.CommittedOffset += bytesWritten;
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
                    state.IsProcessing = false;
                    var previousProgress = state.ProgressSignal;
                    state.ProgressSignal = NewProgressSignal();
                    previousProgress.TrySetException(ex);
                }

                return;
            }
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

    private async Task FinalizeUploadChecksumAsync(string fileId, UploadState state, CancellationToken cancellationToken)
    {
        lock (state.SyncRoot)
        {
            if (state.Fault is not null)
            {
                throw new TusStoreException($"Buffered TUS upload failed for file id {fileId}. {state.Fault.Message}");
            }
        }

        var store = await GetRequiredStore(fileId, cancellationToken);
        var md5 = await GetOrCreateUploadMd5Async(store, fileId, cancellationToken);
        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        if (md5.Hash is not null)
        {
            await storageResolver.SetStagingBlobMd5ChecksumAsync(fileId, md5.Hash, cancellationToken);
        }
    }

    private void CleanupUploadState(string fileId)
    {
        if (_uploadMd5Hashers.TryRemove(fileId, out var hasher))
        {
            hasher.Dispose();
        }

        _uploadLengths.TryRemove(fileId, out _);
        _uploadStates.TryRemove(fileId, out _);
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

    private sealed class UploadState
    {
        public UploadState(long uploadLength, long initialOffset)
        {
            UploadLength = uploadLength;
            AcceptedOffset = initialOffset;
            CommittedOffset = initialOffset;
            ProgressSignal = NewProgressSignal();
        }

        public object SyncRoot { get; } = new();

        public Queue<byte[]> PendingChunks { get; } = new();

        public long UploadLength { get; }

        public long AcceptedOffset { get; set; }

        public long CommittedOffset { get; set; }

        public bool IsProcessing { get; set; }

        public Exception? Fault { get; set; }

        public TaskCompletionSource<long> ProgressSignal { get; set; }
    }
}
