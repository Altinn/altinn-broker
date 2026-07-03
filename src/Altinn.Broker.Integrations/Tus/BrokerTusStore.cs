using System.Collections.Concurrent;

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
    ITusUploadStateRegistry uploadStateRegistry,
    ITusUploadProgressCache uploadProgressCache,
    ITusUploadActivityCache uploadActivityCache,
    IHttpContextAccessor httpContextAccessor,
    IOptions<AzureStorageOptions> azureStorageOptions,
    IOptions<TusOptions> tusOptions) :
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
    private readonly int _maxParallelBlockUploads = Math.Max(azureStorageOptions.Value.ConcurrentUploadThreads, 1);
    private readonly TimeSpan _uploadExpiration = tusOptions.Value.UploadExpiration;

    public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        var state = await GetOrCreateUploadStateAsync(fileId, cancellationToken);
        var durableOffset = await GetDurableUploadOffsetAsync(fileId, cancellationToken);
        bool needsReload;
        lock (state.SyncRoot)
        {
            needsReload = state.AcceptedOffset != durableOffset;
        }

        if (needsReload)
        {
            uploadStateRegistry.Remove(fileId);
            state = await GetOrCreateUploadStateAsync(fileId, cancellationToken);
        }

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
            state.PendingUploads++;
        }

        await UploadBlockAsync(fileId, state, blockId, chunk, cancellationToken);

        lock (state.SyncRoot)
        {
            acceptedOffset = state.AcceptedOffset;
            isFinalChunk = acceptedOffset >= state.UploadLength;
        }

        await PersistProgressAsync(fileId, state, cancellationToken);

        if (isFinalChunk)
        {
            await WaitForCommittedOffsetAsync(fileId, state, acceptedOffset, cancellationToken);
            await FinalizeUploadAsync(fileId, state, cancellationToken);
            await CleanupUploadState(fileId, cancellationToken);
        }
        else
        {
            // In-memory state is per-replica. Release it after each chunk so the next
            // request (possibly on another replica) reads offset from Redis/Azure.
            uploadStateRegistry.Remove(fileId);
        }

        return chunkLength;
    }

    public async Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        if (_uploadLengths.ContainsKey(fileId))
        {
            return true;
        }

        if (uploadStateRegistry.TryGet(fileId, out _))
        {
            return true;
        }

        if (await partialUploadRegistry.IsKnownUploadAsync(fileId, cancellationToken))
        {
            return true;
        }

        if (TusRouteHelper.IsPartialUploadRequest(httpContextAccessor.HttpContext, fileId))
        {
            await TryRestorePartialRegistrationFromBlobAsync(fileId, cancellationToken);
            if (await partialUploadRegistry.IsKnownUploadAsync(fileId, cancellationToken))
            {
                return true;
            }
        }

        if (await uploadProgressCache.GetAsync(fileId, cancellationToken) is not null)
        {
            return true;
        }

        if (await storageResolver.HasStagedBlocksAsync(fileId, cancellationToken))
        {
            return true;
        }

        if (await storageResolver.StagingBlobExistsAsync(fileId, cancellationToken))
        {
            return true;
        }

        if (await storageResolver.TryGetStagingUploadLengthAsync(fileId, cancellationToken) is not null)
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
        fileId = ResolveStoreFileId(fileId);
        if (_uploadLengths.TryGetValue(fileId, out var cachedLength))
        {
            return cachedLength;
        }

        if (await partialUploadRegistry.TryGetUploadLengthAsync(fileId, cancellationToken) is null)
        {
            await TryRestorePartialRegistrationFromBlobAsync(fileId, cancellationToken);
        }

        var registeredLength = await partialUploadRegistry.TryGetUploadLengthAsync(fileId, cancellationToken);
        if (registeredLength is not null)
        {
            await TouchUploadLengthAsync(fileId, registeredLength.Value, cancellationToken);
            await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
            return registeredLength;
        }

        var cachedProgress = await uploadProgressCache.GetAsync(fileId, cancellationToken);
        if (cachedProgress is not null)
        {
            await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
            return cachedProgress.UploadLength;
        }

        var stagingUploadLength = await storageResolver.TryGetStagingUploadLengthAsync(fileId, cancellationToken);
        if (stagingUploadLength is not null)
        {
            await TouchUploadLengthAsync(fileId, stagingUploadLength.Value, cancellationToken);
            await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
            return stagingUploadLength;
        }

        var legacyStore = await GetLegacyAppendBlobStoreIfExistsAsync(fileId, cancellationToken);
        if (legacyStore is not null)
        {
            var legacyLength = await legacyStore.GetUploadLengthAsync(fileId, cancellationToken);
            await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
            return legacyLength;
        }

        var committedLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        var stagedLength = await storageResolver.GetStagedBlocksLengthAsync(fileId, cancellationToken);
        if (committedLength > 0 && stagedLength == 0)
        {
            await TryRestorePartialRegistrationFromBlobAsync(fileId, committedLength, cancellationToken);
            await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
            return committedLength;
        }

        await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
        return null;
    }

    public async Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        await TryFinalizeCompletedPartialAsync(fileId, cancellationToken);

        var offset = await GetDurableUploadOffsetAsync(fileId, cancellationToken);

        if (uploadStateRegistry.TryGet(fileId, out var state))
        {
            lock (state!.SyncRoot)
            {
                if (state.Fault is not null)
                {
                    throw new TusStoreException($"Buffered TUS upload failed for file id {fileId}. {state.Fault.Message}");
                }

                offset = Math.Max(offset, state.AcceptedOffset);
            }
        }

        if (await partialUploadRegistry.TryGetUploadLengthAsync(fileId, cancellationToken) is long uploadLength
            && offset >= uploadLength)
        {
            offset = uploadLength;
        }

        await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
        return offset;
    }

    private async Task<long> GetDurableUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
    {
        var cachedProgress = await uploadProgressCache.GetAsync(fileId, cancellationToken);
        var offset = cachedProgress?.AcceptedOffset ?? 0L;

        var stagedOffset = await storageResolver.GetStagedBlocksLengthAsync(fileId, cancellationToken);
        var committedOffset = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        if (stagedOffset > 0 || committedOffset > 0)
        {
            offset = Math.Max(offset, Math.Max(stagedOffset, committedOffset));
            await TryRestorePartialRegistrationFromStagedUploadAsync(fileId, offset, cancellationToken);
            return offset;
        }

        if (offset > 0)
        {
            return offset;
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

    public async Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
    {
        var fileId = GetFileTransferIdFromRoute();
        _uploadLengths[fileId] = uploadLength;
        _uploadMetadata[fileId] = metadata;
        await partialUploadRegistry.RegisterUploadAsync(fileId, uploadLength, cancellationToken);
        var state = RegisterUploadState(fileId, uploadLength, initialOffset: 0);
        await PersistProgressAsync(fileId, state, cancellationToken);
        uploadStateRegistry.Remove(fileId);
        return fileId;
    }

    public async Task<string> CreatePartialFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
    {
        var fileTransferId = GetFileTransferIdFromRouteGuid();
        var partialFileId = Guid.NewGuid().ToString("N");
        await partialUploadRegistry.RegisterPartialAsync(partialFileId, fileTransferId, uploadLength, cancellationToken);
        await storageResolver.InitializePartialStagingBlobAsync(partialFileId, uploadLength, cancellationToken);
        _uploadLengths[partialFileId] = uploadLength;
        _uploadMetadata[partialFileId] = metadata;
        var state = RegisterUploadState(partialFileId, uploadLength, initialOffset: 0);
        await PersistProgressAsync(partialFileId, state, cancellationToken);
        uploadStateRegistry.Remove(partialFileId);
        return partialFileId;
    }

    public async Task<string> CreateFinalFileAsync(string[] partialFiles, string metadata, CancellationToken cancellationToken)
    {
        var fileTransferId = GetFileTransferIdFromRouteGuid();
        var finalFileId = fileTransferId.ToString();

        long totalLength = 0;
        foreach (var partialFileReference in partialFiles)
        {
            var partialFileId = TusRouteHelper.NormalizePartialFileId(partialFileReference);
            await TryFinalizeCompletedPartialAsync(partialFileId, cancellationToken);

            var partialInfo = await partialUploadRegistry.TryGetPartialInfoAsync(partialFileId, cancellationToken);
            if (partialInfo is null)
            {
                throw new TusStoreException($"Unknown partial upload id {partialFileId}.");
            }

            var (partialFileTransferId, partialLength) = partialInfo.Value;

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

        var normalizedPartialFileIds = partialFiles.Select(TusRouteHelper.NormalizePartialFileId).ToArray();
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
        await partialUploadRegistry.RegisterUploadAsync(finalFileId, totalLength, cancellationToken);
        await partialUploadRegistry.RegisterFinalConcatAsync(finalFileId, normalizedPartialFileIds, cancellationToken);
        var state = RegisterUploadState(finalFileId, totalLength, initialOffset: totalLength);
        state.AcceptedOffset = totalLength;
        state.CommittedOffset = totalLength;
        await PersistProgressAsync(finalFileId, state, cancellationToken);

        foreach (var partialFileId in normalizedPartialFileIds)
        {
            await partialUploadRegistry.RemovePartialAsync(partialFileId, cancellationToken);
        }

        return finalFileId;
    }

    public async Task<FileConcat?> GetUploadConcatAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        if (!await partialUploadRegistry.IsPartialAsync(fileId, cancellationToken))
        {
            await TryRestorePartialRegistrationFromBlobAsync(fileId, cancellationToken);
        }

        if (await partialUploadRegistry.IsPartialAsync(fileId, cancellationToken))
        {
            return new FileConcatPartial();
        }

        var partialFiles = await partialUploadRegistry.TryGetFinalConcatPartialIdsAsync(fileId, cancellationToken);
        if (partialFiles is not null)
        {
            return new FileConcatFinal(partialFiles);
        }

        return null;
    }

    public async Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        if (_uploadMetadata.TryGetValue(fileId, out var metadata))
        {
            return metadata;
        }

        if (await partialUploadRegistry.IsKnownUploadAsync(fileId, cancellationToken))
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
        fileId = ResolveStoreFileId(fileId);
        var legacyStore = await GetLegacyAppendBlobStoreIfExistsAsync(fileId, cancellationToken);
        if (legacyStore is not null)
        {
            return await legacyStore.GetFileAsync(fileId, cancellationToken);
        }

        throw new TusStoreException($"No TUS file found for file id {fileId}");
    }

    public async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        await storageResolver.DeleteStagingBlobAsync(fileId, cancellationToken);

        var legacyStore = await GetLegacyAppendBlobStoreIfExistsAsync(fileId, cancellationToken);
        if (legacyStore is not null)
        {
            await legacyStore.DeleteFileAsync(fileId, cancellationToken);
        }

        await partialUploadRegistry.RemovePartialAsync(fileId, cancellationToken);
        await partialUploadRegistry.RemoveUploadAsync(fileId, cancellationToken);
        await partialUploadRegistry.RemoveFinalConcatAsync(fileId, cancellationToken);
        _uploadMetadata.TryRemove(fileId, out _);
        await CleanupUploadState(fileId, cancellationToken);

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
                var normalizedFileId = TusRouteHelper.NormalizePartialFileId(fileId);
                if (await HasDurableUploadStateAsync(normalizedFileId, cancellationToken))
                {
                    await RenewExpirationAsync(fileId, cancellationToken);
                    continue;
                }

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
        fileId = ResolveStoreFileId(fileId);
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

    private async Task<TusUploadState> GetOrCreateUploadStateAsync(string fileId, CancellationToken cancellationToken)
    {
        var cachedProgress = await uploadProgressCache.GetAsync(fileId, cancellationToken);
        var durableOffset = await GetDurableUploadOffsetAsync(fileId, cancellationToken);

        if (uploadStateRegistry.TryGet(fileId, out var existingState))
        {
            bool shouldRefresh;
            lock (existingState!.SyncRoot)
            {
                shouldRefresh = existingState.AcceptedOffset < durableOffset
                    || (cachedProgress is not null
                        && (existingState.AcceptedOffset < cachedProgress.AcceptedOffset
                            || existingState.NextBlockIndex < cachedProgress.NextBlockIndex));
            }

            if (!shouldRefresh)
            {
                return existingState!;
            }

            uploadStateRegistry.Remove(fileId);
        }

        if (cachedProgress is not null)
        {
            return uploadStateRegistry.GetOrAdd(fileId, () => CreateStateFromSnapshot(cachedProgress));
        }

        var uploadLength = await GetCachedUploadLengthAsync(fileId, cancellationToken);
        var stagedSnapshot = await storageResolver.TryGetStagedBlocksSnapshotAsync(fileId, cancellationToken);
        if (stagedSnapshot is not null)
        {
            return uploadStateRegistry.GetOrAdd(
                fileId,
                () => CreateStateFromStagedBlocks(uploadLength, stagedSnapshot));
        }

        return uploadStateRegistry.GetOrAdd(
            fileId,
            () => new TusUploadState(uploadLength, durableOffset, _maxParallelBlockUploads));
    }

    private TusUploadState RegisterUploadState(string fileId, long uploadLength, long initialOffset)
        => uploadStateRegistry.GetOrAdd(fileId, () => new TusUploadState(uploadLength, initialOffset, _maxParallelBlockUploads));

    private TusUploadState CreateStateFromSnapshot(TusUploadProgressSnapshot snapshot)
    {
        var state = new TusUploadState(snapshot.UploadLength, snapshot.AcceptedOffset, _maxParallelBlockUploads);
        state.AcceptedOffset = snapshot.AcceptedOffset;
        state.CommittedOffset = snapshot.CommittedOffset;
        state.NextBlockIndex = snapshot.NextBlockIndex;
        state.BlockIds.AddRange(snapshot.BlockIds);
        return state;
    }

    private TusUploadState CreateStateFromStagedBlocks(long uploadLength, TusStagedBlocksSnapshot stagedSnapshot)
    {
        var state = new TusUploadState(uploadLength, stagedSnapshot.TotalLength, _maxParallelBlockUploads);
        state.AcceptedOffset = stagedSnapshot.TotalLength;
        state.CommittedOffset = stagedSnapshot.TotalLength;
        state.NextBlockIndex = stagedSnapshot.NextBlockIndex;
        state.BlockIds.AddRange(stagedSnapshot.BlockIds);
        return state;
    }

    private async Task PersistProgressAsync(string fileId, TusUploadState state, CancellationToken cancellationToken)
    {
        TusUploadProgressSnapshot snapshot;
        lock (state.SyncRoot)
        {
            snapshot = new TusUploadProgressSnapshot(
                state.UploadLength,
                state.AcceptedOffset,
                state.CommittedOffset,
                state.NextBlockIndex,
                state.BlockIds.ToList());
        }

        await uploadProgressCache.SaveAsync(fileId, snapshot, cancellationToken);
        await RefreshPartialRegistryAsync(fileId, snapshot.UploadLength, cancellationToken);
        await RecordUploadActivityAsync(fileId, cancellationToken);
    }

    private Task TouchUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
        => Task.WhenAll(
            partialUploadRegistry.RegisterUploadAsync(fileId, uploadLength, cancellationToken),
            RefreshPartialRegistryAsync(fileId, uploadLength, cancellationToken),
            storageResolver.SetStagingUploadLengthAsync(fileId, uploadLength, cancellationToken));

    private Task TryRestorePartialRegistrationFromBlobAsync(string fileId, CancellationToken cancellationToken)
        => TryRestorePartialRegistrationFromBlobAsync(fileId, uploadLength: null, cancellationToken);

    private async Task TryRestorePartialRegistrationFromBlobAsync(
        string fileId,
        long? uploadLength,
        CancellationToken cancellationToken)
    {
        if (await partialUploadRegistry.TryGetPartialInfoAsync(fileId, cancellationToken) is not null)
        {
            return;
        }

        uploadLength ??= await storageResolver.TryGetStagingUploadLengthAsync(fileId, cancellationToken);
        if (uploadLength is null)
        {
            var committedLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
            var stagedLength = await storageResolver.GetStagedBlocksLengthAsync(fileId, cancellationToken);
            if (committedLength > 0 && stagedLength == 0)
            {
                uploadLength = committedLength;
            }
            else
            {
                return;
            }
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null || !TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var fileTransferId))
        {
            return;
        }

        await partialUploadRegistry.RegisterPartialAsync(fileId, fileTransferId, uploadLength.Value, cancellationToken);
        await uploadActivityCache.RecordActivityAsync(fileTransferId, cancellationToken);
    }

    private async Task TryRestorePartialRegistrationFromStagedUploadAsync(
        string fileId,
        long stagedOffset,
        CancellationToken cancellationToken)
    {
        if (await partialUploadRegistry.TryGetPartialInfoAsync(fileId, cancellationToken) is not null)
        {
            return;
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null
            || !TusRouteHelper.IsPartialUploadPath(TusRouteHelper.GetRequestPath(httpContext))
            || !TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var fileTransferId))
        {
            return;
        }

        var uploadLength = await partialUploadRegistry.TryGetUploadLengthAsync(fileId, cancellationToken)
            ?? await storageResolver.TryGetStagingUploadLengthAsync(fileId, cancellationToken);
        if (uploadLength is null)
        {
            return;
        }

        await partialUploadRegistry.RegisterPartialAsync(fileId, fileTransferId, uploadLength.Value, cancellationToken);
        await uploadActivityCache.RecordActivityAsync(fileTransferId, cancellationToken);
    }

    private async Task RefreshPartialRegistryAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
    {
        var existingPartial = await partialUploadRegistry.TryGetPartialInfoAsync(fileId, cancellationToken);
        if (existingPartial is not null)
        {
            await partialUploadRegistry.RegisterPartialAsync(
                fileId,
                existingPartial.Value.FileTransferId,
                existingPartial.Value.UploadLength,
                cancellationToken);
            return;
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null || !TusRouteHelper.IsPartialUploadPath(TusRouteHelper.GetRequestPath(httpContext)))
        {
            return;
        }

        if (!TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var fileTransferId))
        {
            return;
        }

        await partialUploadRegistry.RegisterPartialAsync(fileId, fileTransferId, uploadLength, cancellationToken);
    }

    private async Task RecordUploadActivityAsync(string fileId, CancellationToken cancellationToken)
    {
        if (await partialUploadRegistry.TryGetFileTransferIdAsync(fileId, cancellationToken) is Guid mappedFileTransferId)
        {
            await uploadActivityCache.RecordActivityAsync(mappedFileTransferId, cancellationToken);
            return;
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null
            && TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var routeFileTransferId))
        {
            await uploadActivityCache.RecordActivityAsync(routeFileTransferId, cancellationToken);
            return;
        }

        if (Guid.TryParse(fileId, out var fileTransferId))
        {
            await uploadActivityCache.RecordActivityAsync(fileTransferId, cancellationToken);
        }
    }

    private async Task UploadBlockAsync(
        string fileId,
        TusUploadState state,
        string blockId,
        byte[] chunk,
        CancellationToken cancellationToken)
    {
        await state.ConcurrentUploader.WaitAsync(cancellationToken);
        try
        {
            await storageResolver.StageTusBlockAsync(fileId, blockId, chunk, cancellationToken);
            lock (state.SyncRoot)
            {
                state.AcceptedOffset += chunk.Length;
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

            throw new TusStoreException($"Buffered TUS upload failed for file id {fileId}. {ex.Message}");
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
        TusUploadState state,
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

    private async Task TryFinalizeCompletedPartialAsync(string fileId, CancellationToken cancellationToken)
    {
        if (!await partialUploadRegistry.IsPartialAsync(fileId, cancellationToken))
        {
            return;
        }

        var uploadLength = await partialUploadRegistry.TryGetUploadLengthAsync(fileId, cancellationToken);
        if (uploadLength is null)
        {
            return;
        }

        var committedLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        if (committedLength >= uploadLength.Value)
        {
            return;
        }

        var stagedSnapshot = await storageResolver.TryGetStagedBlocksSnapshotAsync(fileId, cancellationToken);
        if (stagedSnapshot is null || stagedSnapshot.TotalLength < uploadLength.Value)
        {
            return;
        }

        var state = CreateStateFromStagedBlocks(uploadLength.Value, stagedSnapshot);
        await FinalizeUploadAsync(fileId, state, cancellationToken);
        await PersistProgressAsync(fileId, state, cancellationToken);
        uploadStateRegistry.Remove(fileId);
    }

    private async Task FinalizeUploadAsync(string fileId, TusUploadState state, CancellationToken cancellationToken)
    {
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

            blockIds = state.BlockIds.ToList();
        }

        if (blockIds.Count == 0)
        {
            throw new TusStoreException($"Cannot finalize TUS upload for file id {fileId} because no blocks were staged.");
        }

        var uploadLength = state.UploadLength;
        await storageResolver.CommitTusBlocksAsync(fileId, blockIds, cancellationToken);
        var md5Hash = await storageResolver.ComputeCommittedStagingMd5Async(fileId, cancellationToken);
        await storageResolver.SetCommittedStagingMd5Async(fileId, md5Hash, cancellationToken);
        await storageResolver.SetStagingUploadLengthAsync(fileId, uploadLength, cancellationToken);
    }

    private async Task CleanupUploadState(string fileId, CancellationToken cancellationToken)
    {
        uploadStateRegistry.Remove(fileId);
        await uploadProgressCache.RemoveAsync(fileId, cancellationToken);

        if (!await partialUploadRegistry.IsPartialAsync(fileId, cancellationToken))
        {
            _uploadLengths.TryRemove(fileId, out _);
        }
    }

    private async Task<bool> HasDurableUploadStateAsync(string fileId, CancellationToken cancellationToken)
        => await uploadProgressCache.GetAsync(fileId, cancellationToken) is not null
            || await partialUploadRegistry.IsKnownUploadAsync(fileId, cancellationToken)
            || await storageResolver.StagingBlobExistsAsync(fileId, cancellationToken)
            || await storageResolver.HasStagedBlocksAsync(fileId, cancellationToken);

    private async Task RenewExpirationAsync(string fileId, CancellationToken cancellationToken)
    {
        await SetExpirationAsync(fileId, DateTimeOffset.UtcNow.Add(_uploadExpiration), cancellationToken);

        var normalizedFileId = TusRouteHelper.NormalizePartialFileId(fileId);
        if (await partialUploadRegistry.TryGetFileTransferIdAsync(normalizedFileId, cancellationToken) is Guid fileTransferId)
        {
            await uploadActivityCache.RecordActivityAsync(fileTransferId, cancellationToken);
        }
        else if (Guid.TryParse(normalizedFileId, out fileTransferId))
        {
            await uploadActivityCache.RecordActivityAsync(fileTransferId, cancellationToken);
        }
    }

    private async Task RenewExpirationIfTrackedAsync(string fileId, CancellationToken cancellationToken)
    {
        if (await GetExpirationAsync(fileId, cancellationToken) is not null)
        {
            await RenewExpirationAsync(fileId, cancellationToken);
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

    private static string ResolveStoreFileId(string fileId) => TusRouteHelper.NormalizePartialFileId(fileId);

    private static string BuildBlockId(long blockIndex)
    {
        var blockId = blockIndex.ToString("D12");
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(blockId));
    }
}
