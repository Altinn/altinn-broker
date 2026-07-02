using System.Collections.Concurrent;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;

using Altinn.Broker.Core.Options;
using Xtensible.TusDotNet.Azure;

namespace Altinn.Broker.Integrations.Tus;

public class BrokerTusStore(
    ITusStorageResolver storageResolver,
    ITusExpirationDetailsStore expirationDetailsStore,
    ITusPartialUploadRegistry partialUploadRegistry,
    IHttpContextAccessor httpContextAccessor,
    IOptions<AzureStorageOptions> azureStorageOptions) :
    ITusStore,
    ITusCreationStore,
    ITusReadableStore,
    ITusTerminationStore,
    ITusExpirationStore,
    ITusChecksumStore,
    ITusConcatenationStore
{
    private readonly ConcurrentDictionary<string, long> _uploadLengths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _uploadMetadata = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, UploadState> _uploadStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string[]> _finalConcatPartials = new(StringComparer.OrdinalIgnoreCase);

    public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
    {
        var state = await GetOrCreateUploadStateAsync(fileId, cancellationToken);

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
        if (_uploadLengths.ContainsKey(fileId) || _uploadStates.ContainsKey(fileId))
        {
            return true;
        }

        if (partialUploadRegistry.IsKnownUpload(fileId))
        {
            return true;
        }

        if (await storageResolver.StagingBlobExistsAsync(fileId, cancellationToken))
        {
            return true;
        }

        if (await storageResolver.DestinationBlobExistsAsync(fileId, cancellationToken))
        {
            return true;
        }

        var legacyStore = await GetLegacyAppendBlobStoreIfExistsAsync(fileId, cancellationToken);
        return legacyStore is not null;
    }

    public async Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        if (_uploadLengths.TryGetValue(fileId, out var cachedLength))
        {
            return cachedLength;
        }

        if (partialUploadRegistry.TryGetUploadLength(fileId, out var registeredLength))
        {
            return registeredLength;
        }

        var legacyStore = await GetLegacyAppendBlobStoreIfExistsAsync(fileId, cancellationToken);
        if (legacyStore is not null)
        {
            return await legacyStore.GetUploadLengthAsync(fileId, cancellationToken);
        }

        return null;
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

        var committedLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        if (committedLength > 0)
        {
            return committedLength;
        }

        var destinationLength = await storageResolver.GetDestinationBlobLengthAsync(fileId, cancellationToken);
        if (destinationLength > 0)
        {
            return destinationLength;
        }

        var legacyStore = await GetLegacyAppendBlobStoreIfExistsAsync(fileId, cancellationToken);
        if (legacyStore is not null)
        {
            return await legacyStore.GetUploadOffsetAsync(fileId, cancellationToken);
        }

        return 0;
    }

    public Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
    {
        var fileId = GetFileTransferIdFromRoute();
        _uploadLengths[fileId] = uploadLength;
        _uploadMetadata[fileId] = metadata;
        partialUploadRegistry.RegisterUpload(fileId, uploadLength);
        _uploadStates[fileId] = new UploadState(uploadLength, initialOffset: 0, azureStorageOptions.Value.ConcurrentUploadThreads);
        return Task.FromResult(fileId);
    }

    public Task<string> CreatePartialFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
    {
        var fileTransferId = GetFileTransferIdFromRouteGuid();
        var partialFileId = Guid.NewGuid().ToString("N");
        partialUploadRegistry.RegisterPartial(partialFileId, fileTransferId, uploadLength);
        _uploadLengths[partialFileId] = uploadLength;
        _uploadMetadata[partialFileId] = metadata;
        _uploadStates[partialFileId] = new UploadState(uploadLength, initialOffset: 0, azureStorageOptions.Value.ConcurrentUploadThreads);
        return Task.FromResult(partialFileId);
    }

    public async Task<string> CreateFinalFileAsync(string[] partialFiles, string metadata, CancellationToken cancellationToken)
    {
        var fileTransferId = GetFileTransferIdFromRouteGuid();
        var finalFileId = fileTransferId.ToString();

        long totalLength = 0;
        foreach (var partialFileReference in partialFiles)
        {
            var partialFileId = NormalizePartialFileId(partialFileReference);
            if (!partialUploadRegistry.TryGetPartialInfo(partialFileId, out var partialFileTransferId, out var partialLength))
            {
                throw new TusStoreException($"Unknown partial upload id {partialFileId}.");
            }

            if (partialFileTransferId != fileTransferId)
            {
                throw new TusStoreException($"Partial upload {partialFileId} does not belong to file transfer {fileTransferId}.");
            }

            var committedLength = await storageResolver.GetCommittedStagingLengthAsync(partialFileId, cancellationToken);
            if (committedLength != partialLength)
            {
                throw new TusStoreException(
                    $"Partial upload {partialFileId} is incomplete. Expected {partialLength} bytes, found {committedLength}.");
            }

            totalLength += partialLength;
        }

        var normalizedPartialFileIds = partialFiles.Select(NormalizePartialFileId).ToArray();
        var concatenatedLength = await storageResolver.ConcatenatePartialStagingBlobsAsync(
            finalFileId,
            normalizedPartialFileIds,
            cancellationToken);
        if (concatenatedLength != totalLength)
        {
            throw new TusStoreException(
                $"Concatenated upload length mismatch for file transfer {fileTransferId}. Expected {totalLength}, got {concatenatedLength}.");
        }

        _uploadLengths[finalFileId] = totalLength;
        _uploadMetadata[finalFileId] = metadata;
        partialUploadRegistry.RegisterUpload(finalFileId, totalLength);
        _finalConcatPartials[finalFileId] = normalizedPartialFileIds;
        _uploadStates[finalFileId] = new UploadState(
            totalLength,
            initialOffset: totalLength,
            azureStorageOptions.Value.ConcurrentUploadThreads)
        {
            AcceptedOffset = totalLength,
            CommittedOffset = totalLength
        };

        foreach (var partialFileId in normalizedPartialFileIds)
        {
            partialUploadRegistry.RemovePartial(partialFileId);
        }

        return finalFileId;
    }

    public Task<FileConcat?> GetUploadConcatAsync(string fileId, CancellationToken cancellationToken)
    {
        if (partialUploadRegistry.IsPartial(fileId))
        {
            return Task.FromResult<FileConcat?>(new FileConcatPartial());
        }

        if (_finalConcatPartials.TryGetValue(fileId, out var partialFiles))
        {
            return Task.FromResult<FileConcat?>(new FileConcatFinal(partialFiles));
        }

        return Task.FromResult<FileConcat?>(null);
    }

    public async Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken)
    {
        if (_uploadMetadata.TryGetValue(fileId, out var metadata))
        {
            return metadata;
        }

        if (partialUploadRegistry.IsKnownUpload(fileId))
        {
            return string.Empty;
        }

        var legacyStore = await GetLegacyAppendBlobStoreIfExistsAsync(fileId, cancellationToken);
        if (legacyStore is not null)
        {
            return await legacyStore.GetUploadMetadataAsync(fileId, cancellationToken);
        }

        return string.Empty;
    }

    public async Task<ITusFile> GetFileAsync(string fileId, CancellationToken cancellationToken)
    {
        var legacyStore = await GetLegacyAppendBlobStoreIfExistsAsync(fileId, cancellationToken);
        if (legacyStore is not null)
        {
            return await legacyStore.GetFileAsync(fileId, cancellationToken);
        }

        throw new TusStoreException($"No TUS file found for file id {fileId}");
    }

    public async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
    {
        await storageResolver.DeleteStagingBlobAsync(fileId, cancellationToken);

        var legacyStore = await GetLegacyAppendBlobStoreIfExistsAsync(fileId, cancellationToken);
        if (legacyStore is not null)
        {
            await legacyStore.DeleteFileAsync(fileId, cancellationToken);
        }

        partialUploadRegistry.RemovePartial(fileId);
        partialUploadRegistry.RemoveUpload(fileId);
        _finalConcatPartials.TryRemove(fileId, out _);
        _uploadMetadata.TryRemove(fileId, out _);
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
        var legacyStore = await GetLegacyAppendBlobStoreIfExistsAsync(fileId, cancellationToken);
        if (legacyStore is not null)
        {
            return await legacyStore.VerifyChecksumAsync(fileId, algorithm, checksum, cancellationToken);
        }

        return false;
    }

    private async Task<AzureBlobTusStore?> GetLegacyAppendBlobStoreIfExistsAsync(
        string fileId,
        CancellationToken cancellationToken)
    {
        var store = await storageResolver.GetStoreForFileAsync(fileId, cancellationToken);
        if (store is null || !await store.FileExistAsync(fileId, cancellationToken))
        {
            return null;
        }

        return store;
    }

    private async Task<long> GetCachedUploadLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        if (_uploadLengths.TryGetValue(fileId, out var cachedLength))
        {
            return cachedLength;
        }

        var uploadLength = await GetUploadLengthAsync(fileId, cancellationToken)
            ?? throw new TusStoreException($"Upload length is missing for file id {fileId}");
        _uploadLengths[fileId] = uploadLength;
        return uploadLength;
    }

    private async Task<UploadState> GetOrCreateUploadStateAsync(string fileId, CancellationToken cancellationToken)
    {
        if (_uploadStates.TryGetValue(fileId, out var existingState))
        {
            return existingState;
        }

        var uploadLength = await GetCachedUploadLengthAsync(fileId, cancellationToken);
        var currentOffset = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
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

        if (!partialUploadRegistry.IsPartial(fileId))
        {
            _uploadLengths.TryRemove(fileId, out _);
        }
    }

    private string GetFileTransferIdFromRoute()
        => GetFileTransferIdFromRouteGuid().ToString();

    private Guid GetFileTransferIdFromRouteGuid()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new TusStoreException("Missing HTTP context");

        if (!TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var fileTransferId))
        {
            throw new TusStoreException("Missing file transfer id in route");
        }

        return fileTransferId;
    }

    private static string NormalizePartialFileId(string partialFileReference)
    {
        var trimmedReference = partialFileReference.Trim();
        if (!trimmedReference.Contains('/'))
        {
            return trimmedReference;
        }

        return trimmedReference.TrimEnd('/').Split('/').Last();
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
