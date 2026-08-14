using System.Collections.Concurrent;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;

using Altinn.Broker.Application.UploadFile.Tus;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Integrations.Azure;

using Xtensible.TusDotNet.Azure;

namespace Altinn.Broker.Integrations.Tus;

public class BrokerTusStore(
    ITusStorageResolver storageResolver,
    ITusExpirationDetailsStore expirationDetailsStore,
    ITusPartialUploadRegistry partialUploadRegistry,
    ITusUploadStateRegistry uploadStateRegistry,
    ITusUploadProgressCache uploadProgressCache,
    ITusUploadActivityCache uploadActivityCache,
    ITusConcatCheckpointStore concatCheckpointStore,
    IHttpContextAccessor httpContextAccessor,
    ILogger<BrokerTusStore> logger,
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
    private readonly int _maxBlocksPerStripe = Math.Max(azureStorageOptions.Value.MaxBlocksPerStripe, 1);
    private readonly TimeSpan _uploadExpiration = tusOptions.Value.UploadExpiration;

    public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        using var timing = TusUploadDebugTiming.Start(logger, "AppendData", fileId);

        var uploadLength = await GetCachedUploadLengthAsync(fileId, cancellationToken);
        var state = await GetOrCreateUploadStateAsync(fileId, cancellationToken);
        timing.Step("getOrCreateUploadState");

        using var chunkBuffer = new MemoryStream();
        await stream.CopyToAsync(chunkBuffer, cancellationToken);
        timing.Step("readRequestBody", chunkBuffer.Length);
        if (chunkBuffer.Length == 0)
        {
            return 0;
        }

        if (chunkBuffer.Length > int.MaxValue)
        {
            throw new TusStoreException($"TUS chunk size exceeds maximum supported size for file id {fileId}.");
        }

        var chunk = chunkBuffer.ToArray();
        var chunkLength = chunk.Length;
        var progress = await uploadProgressCache.GetAsync(fileId, cancellationToken);
        var expectedOffset = progress?.AcceptedOffset ?? state.AcceptedOffset;

        // The base offset turns a partial's PATCH offset into the absolute offset that picks the stripe.
        var partialInfo = await partialUploadRegistry.TryGetPartialInfoAsync(fileId, cancellationToken);
        var layout = await GetStripeLayoutAsync(fileId, partialInfo, uploadLength, cancellationToken);
        var baseOffset = partialInfo?.BaseOffset ?? 0;
        var fragments = layout.SplitAcrossStripes(baseOffset + expectedOffset, chunkLength);
        timing.Step("splitAcrossStripes", fragments.Count);

        EnsureChunkFitsBlockBudget(fileId, layout, expectedOffset, chunkLength);

        var acceptResult = await uploadProgressCache.TryAcceptChunkAsync(
            fileId,
            expectedOffset,
            chunkLength,
            fragments.Count,
            cancellationToken);
        if (acceptResult.Status == TusAcceptChunkStatus.NotFound)
        {
            await uploadProgressCache.InitializeAsync(fileId, uploadLength, cancellationToken);
            acceptResult = await uploadProgressCache.TryAcceptChunkAsync(
                fileId,
                expectedOffset,
                chunkLength,
                fragments.Count,
                cancellationToken);
        }

        timing.Step("acceptChunk.redis", expectedOffset);
        if (acceptResult.Status == TusAcceptChunkStatus.Conflict)
        {
            throw new TusStoreException(
                $"Upload offset conflict for file id {fileId}. Expected {expectedOffset}, current {acceptResult.CurrentAcceptedOffset}.");
        }

        if (acceptResult.Status == TusAcceptChunkStatus.Overflow)
        {
            throw new TusStoreException(
                $"Upload chunk exceeds upload length for file id {fileId} at offset {expectedOffset}.");
        }

        if (acceptResult.Status != TusAcceptChunkStatus.Accepted)
        {
            throw new TusStoreException($"Unable to accept TUS chunk for file id {fileId} at offset {expectedOffset}.");
        }

        await EnsureStripeBlockBudgetAsync(fileId, partialInfo, layout, fragments, cancellationToken);

        var blockIds = new string[fragments.Count];
        long acceptedOffset;
        bool isFinalChunk;

        lock (state.SyncRoot)
        {
            if (state.Fault is not null)
            {
                throw new TusStoreException($"Buffered TUS upload failed for file id {fileId}. {state.Fault.Message}");
            }

            for (var i = 0; i < fragments.Count; i++)
            {
                blockIds[i] = partialInfo is not null
                    ? TusBlockIds.BuildNamespacedBlockId(partialInfo.Value.PartialIndex, acceptResult.BlockIndex + i)
                    : TusBlockIds.BuildSequentialBlockId(acceptResult.BlockIndex + i);
                state.AddBlockId(fragments[i].StripeIndex, blockIds[i]);
            }

            state.AcceptedOffset = acceptResult.NewAcceptedOffset;
            state.NextBlockIndex = acceptResult.BlockIndex + fragments.Count;
            acceptedOffset = acceptResult.NewAcceptedOffset;
            isFinalChunk = acceptedOffset >= state.UploadLength;
            state.PendingUploads += fragments.Count;
        }

        timing.Step("assignBlocks", fragments.Count);
        var destinationFileId = partialInfo?.FileTransferId.ToString();
        for (var i = 0; i < fragments.Count; i++)
        {
            var fragment = fragments[i];
            var blockId = blockIds[i];
            _ = Task.Run(() => UploadBlockAsync(fileId, destinationFileId, fragment.StripeIndex, state, blockId, chunk, fragment.SourceOffset, fragment.Length));
        }

        timing.Step("uploadBlock.scheduled", chunkLength);
        await RecordUploadActivityAsync(fileId, cancellationToken);

        if (isFinalChunk)
        {
            logger.LogInformation(
                "TUS final chunk accepted for file id {FileId}. ChunkBytes={ChunkBytes} AcceptedOffset={AcceptedOffset} UploadLength={UploadLength}",
                fileId,
                chunkLength,
                acceptedOffset,
                state.UploadLength);

            var isPartial = partialInfo is not null;
            if (isPartial)
            {
                await WaitForDurableCommittedOffsetAsync(fileId, state.UploadLength, cancellationToken);
                timing.Step("waitForDurableCommittedOffset");
            }

            await LogUploadCompletionProbeAsync(fileId, acceptedOffset, chunkLength, cancellationToken);
        }

        return chunkLength;
    }

    /// <summary>
    /// For a partial the assembled length is not known yet, so only the stripe size is populated.
    /// That is all <see cref="StripeLayout.SplitAcrossStripes"/> needs.
    /// </summary>
    private async Task<StripeLayout> GetStripeLayoutAsync(
        string fileId,
        PartialUploadInfo? partialInfo,
        long uploadLength,
        CancellationToken cancellationToken)
    {
        var lookupId = partialInfo?.FileTransferId.ToString() ?? fileId;
        var stripeSize = await storageResolver.GetStripeSizeAsync(lookupId, cancellationToken);
        return new StripeLayout(partialInfo is null ? uploadLength : 0, stripeSize);
    }

    /// <summary>
    /// Fails an upload that could never fit on its first chunk rather than hours later at commit time.
    /// </summary>
    private void EnsureChunkFitsBlockBudget(string fileId, StripeLayout layout, long expectedOffset, int chunkLength)
    {
        if (expectedOffset != 0)
        {
            return;
        }

        var minimumChunkSize = layout.MinimumChunkSize(_maxBlocksPerStripe);
        if (chunkLength < minimumChunkSize)
        {
            throw new TusStoreException(
                $"Upload chunk size {chunkLength} is too small for file id {fileId}. " +
                $"Increase TUS chunk size to at least {minimumChunkSize} bytes.");
        }
    }

    /// <summary>
    /// Counted per (file transfer, stripe), not per partial: several partials share one stripe blob,
    /// and the budget is Azure's per-blob limit.
    /// </summary>
    private async Task EnsureStripeBlockBudgetAsync(
        string fileId,
        PartialUploadInfo? partialInfo,
        StripeLayout layout,
        IReadOnlyList<StripeFragment> fragments,
        CancellationToken cancellationToken)
    {
        if (partialInfo is not { } partial)
        {
            return;
        }

        foreach (var fragment in fragments)
        {
            var blockCount = await partialUploadRegistry.IncrementStripeBlockCountAsync(
                partial.FileTransferId,
                fragment.StripeIndex,
                1,
                cancellationToken);
            if (blockCount > _maxBlocksPerStripe)
            {
                throw new TusStoreException(
                    $"Upload exceeds the {_maxBlocksPerStripe} block limit for stripe {fragment.StripeIndex} of " +
                    $"file id {fileId}. Increase TUS chunk size to at least " +
                    $"{layout.MinimumChunkSize(_maxBlocksPerStripe)} bytes.");
            }
        }
    }

    private async Task LogUploadCompletionProbeAsync(
        string fileId,
        long acceptedOffset,
        int lastChunkBytes,
        CancellationToken cancellationToken)
    {
        var storeOffset = await GetUploadOffsetAsync(fileId, cancellationToken);
        var storeLength = await GetUploadLengthAsync(fileId, cancellationToken);
        var uploadConcat = await GetUploadConcatAsync(fileId, cancellationToken);
        var committedStagingLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        var stagedBlocksLength = await storageResolver.GetStagedBlocksLengthAsync(fileId, cancellationToken);
        var destinationCommittedLength = (await storageResolver.GetCommittedStripesAsync(fileId, cancellationToken)).TotalLength;
        var destinationStagedLength = await storageResolver.GetDestinationUncommittedBlocksLengthAsync(fileId, 0, cancellationToken);
        var expectedPatchOffset = acceptedOffset;
        var tusdotnetWillComplete = storeLength is not null
            && expectedPatchOffset == storeLength
            && uploadConcat is not FileConcatPartial;

        logger.LogInformation(
            "TUS completion probe for file id {FileId}. LastChunkBytes={LastChunkBytes} AcceptedOffset={AcceptedOffset} StoreOffset={StoreOffset} StoreLength={StoreLength} CommittedStagingLength={CommittedStagingLength} StagedBlocksLength={StagedBlocksLength} DestinationCommittedLength={DestinationCommittedLength} DestinationStagedLength={DestinationStagedLength} UploadConcat={UploadConcat} TusdotnetShouldCallOnFileComplete={ShouldComplete}",
            fileId,
            lastChunkBytes,
            acceptedOffset,
            storeOffset,
            storeLength,
            committedStagingLength,
            stagedBlocksLength,
            destinationCommittedLength,
            destinationStagedLength,
            DescribeUploadConcat(uploadConcat),
            tusdotnetWillComplete);

        if (!tusdotnetWillComplete)
        {
            if (uploadConcat is FileConcatPartial)
            {
                logger.LogInformation(
                    "TUS partial upload finished for file id {FileId}. OnFileComplete is not expected on partial PATCH; client must POST Upload-Concat final.",
                    fileId);
            }
            else
            {
                logger.LogWarning(
                    "TUS OnFileComplete may be skipped for file id {FileId}. tusdotnet requires StoreOffset to match StoreLength after PATCH and skips partial uploads. PatchNewOffset={PatchNewOffset} StoreOffset={StoreOffset} StoreLength={StoreLength} UploadConcat={UploadConcat}",
                    fileId,
                    acceptedOffset,
                    storeOffset,
                    storeLength,
                    DescribeUploadConcat(uploadConcat));
            }
        }
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

        if ((await storageResolver.GetCommittedStripesAsync(fileId, cancellationToken)).StripeCount > 0)
        {
            return true;
        }

        return false;
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

        var committedLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        if (committedLength > 0)
        {
            await TryRestorePartialRegistrationFromBlobAsync(fileId, committedLength, cancellationToken);
            await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
            return committedLength;
        }

        var stagedLength = await storageResolver.GetStagedBlocksLengthAsync(fileId, cancellationToken);
        if (stagedLength > 0)
        {
            await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
            return stagedLength;
        }

        await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
        return null;
    }

    public async Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        using var timing = TusUploadDebugTiming.Start(logger, "GetUploadOffset", fileId);
        var reportDurableOffset = IsHeadOffsetRequest();

        var cachedProgress = await uploadProgressCache.GetAsync(fileId, cancellationToken);
        if (cachedProgress is not null)
        {
            if (uploadStateRegistry.TryGet(fileId, out var existingState))
            {
                SyncStateFromProgress(existingState!, cachedProgress);
            }

            if (reportDurableOffset)
            {
                var durableOffset = await GetDurableStagingOffsetAsync(fileId, cancellationToken);
                if (durableOffset > 0)
                {
                    timing.Step("durableStagingOffset", durableOffset);
                    await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
                    return durableOffset;
                }
            }

            var offset = reportDurableOffset
                ? cachedProgress.CommittedOffset
                : cachedProgress.AcceptedOffset;
            timing.Step(reportDurableOffset ? "redisCommittedOffset" : "redisAcceptedOffset", offset);
            await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
            return offset;
        }

        timing.Step("redisProgressMiss");

        if (reportDurableOffset)
        {
            var committedLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
            var stagedLength = await storageResolver.GetStagedBlocksLengthAsync(fileId, cancellationToken);
            var durableOffset = committedLength + stagedLength;
            if (durableOffset > 0)
            {
                timing.Step("durableStagingOffset", durableOffset);
                await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
                return durableOffset;
            }

            if (committedLength > 0)
            {
                timing.Step("committedStagingLength", committedLength);
                await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
                return committedLength;
            }
        }
        else if (uploadStateRegistry.TryGet(fileId, out var state))
        {
            timing.Step("inMemoryAcceptedOffset", state!.AcceptedOffset);
            await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
            return state.AcceptedOffset;
        }

        var committedStagingLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        if (committedStagingLength > 0)
        {
            timing.Step("committedStagingLength", committedStagingLength);
            await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
            return committedStagingLength;
        }

        var destinationLength = (await storageResolver.GetCommittedStripesAsync(fileId, cancellationToken)).TotalLength;
        if (destinationLength > 0)
        {
            timing.Step("destinationLength", destinationLength);
            await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
            return destinationLength;
        }

        var uploadLength = await GetUploadLengthAsync(fileId, cancellationToken);
        if (uploadLength is > 0
            && await partialUploadRegistry.TryGetConcatStatusAsync(fileId, cancellationToken) == TusConcatStatus.Complete)
        {
            var concatCommittedLength = (await storageResolver.GetCommittedStripesAsync(fileId, cancellationToken)).TotalLength;
            if (concatCommittedLength >= uploadLength.Value)
            {
                timing.Step("concatCompleteCommittedLength", concatCommittedLength);
                await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
                return uploadLength.Value;
            }
        }

        timing.Step("zero");
        await RenewExpirationIfTrackedAsync(fileId, cancellationToken);
        return 0;
    }

    public async Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
    {
        var fileId = GetFileTransferIdFromRoute();
        _uploadLengths[fileId] = uploadLength;
        _uploadMetadata[fileId] = metadata;
        await partialUploadRegistry.RegisterUploadAsync(fileId, uploadLength, cancellationToken);
        var state = RegisterUploadState(fileId, uploadLength, initialOffset: 0);
        await uploadProgressCache.InitializeAsync(fileId, uploadLength, cancellationToken);
        await RunProgressSideEffectsAsync(fileId, uploadLength, cancellationToken);
        return fileId;
    }

    public async Task<string> CreatePartialFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
    {
        var fileTransferId = GetFileTransferIdFromRouteGuid();
        var partialFileId = Guid.NewGuid().ToString("N");
        await partialUploadRegistry.RegisterPartialAsync(partialFileId, fileTransferId, uploadLength, cancellationToken);
        _uploadLengths[partialFileId] = uploadLength;
        _uploadMetadata[partialFileId] = metadata;
        RegisterUploadState(partialFileId, uploadLength, initialOffset: 0);
        await uploadProgressCache.InitializeAsync(partialFileId, uploadLength, cancellationToken);
        await RunProgressSideEffectsAsync(partialFileId, uploadLength, cancellationToken);
        return partialFileId;
    }

    public async Task<string> CreateFinalFileAsync(string[] partialFiles, string metadata, CancellationToken cancellationToken)
    {
        var fileTransferId = GetFileTransferIdFromRouteGuid();
        var finalFileId = fileTransferId.ToString();
        var stripeSize = await storageResolver.GetStripeSizeAsync(finalFileId, cancellationToken);

        long totalLength = 0;
        var previousPartialIndex = -1;
        foreach (var partialFileReference in partialFiles)
        {
            var partialFileId = TusRouteHelper.NormalizePartialFileId(partialFileReference);
            var partialInfo = await partialUploadRegistry.TryGetPartialInfoAsync(partialFileId, cancellationToken);
            if (partialInfo is null)
            {
                throw new TusStoreException($"Unknown partial upload id {partialFileId}.");
            }

            var partialFileTransferId = partialInfo.Value.FileTransferId;
            var partialLength = partialInfo.Value.UploadLength;

            if (partialFileTransferId != fileTransferId)
            {
                throw new TusStoreException($"Partial upload {partialFileId} does not belong to file transfer {fileTransferId}.");
            }

            if (partialInfo.Value.PartialIndex <= previousPartialIndex)
            {
                throw new TusStoreException(
                    $"Partial uploads must be listed in the order they were created. Partial {partialFileId} " +
                    $"has index {partialInfo.Value.PartialIndex}, which does not follow {previousPartialIndex}.");
            }

            if (stripeSize > 0 && partialInfo.Value.BaseOffset != totalLength)
            {
                throw new TusStoreException(
                    $"Partial upload {partialFileId} starts at offset {partialInfo.Value.BaseOffset} but " +
                    $"{totalLength} bytes precede it. The concatenation is missing a partial.");
            }

            previousPartialIndex = partialInfo.Value.PartialIndex;
            await EnsurePartialReadyForConcatenationAsync(partialFileId, partialLength, cancellationToken);
            totalLength += partialLength;
        }

        var normalizedPartialFileIds = partialFiles.Select(TusRouteHelper.NormalizePartialFileId).ToArray();

        _uploadLengths[finalFileId] = totalLength;
        _uploadMetadata[finalFileId] = metadata;
        await partialUploadRegistry.RegisterUploadAsync(finalFileId, totalLength, cancellationToken);
        await partialUploadRegistry.RegisterFinalConcatAsync(finalFileId, normalizedPartialFileIds, cancellationToken);

        var layout = new StripeLayout(totalLength, stripeSize);
        await concatCheckpointStore.SaveCheckpointAsync(
            finalFileId,
            new TusConcatCheckpoint(StripeSizeBytes: layout.StripeSize, StripeCount: layout.StripeCount),
            cancellationToken);
        await storageResolver.SetDestinationUploadLengthAsync(finalFileId, totalLength, cancellationToken);
        var state = RegisterUploadState(finalFileId, totalLength, initialOffset: totalLength);
        state.AcceptedOffset = totalLength;
        state.CommittedOffset = totalLength;
        await uploadProgressCache.SaveAsync(
            finalFileId,
            new TusUploadProgressSnapshot(totalLength, totalLength, totalLength, state.NextBlockIndex),
            cancellationToken);
        await RunProgressSideEffectsAsync(finalFileId, totalLength, cancellationToken);

        logger.LogInformation(
            "TUS concatenation final accepted for file transfer {FileTransferId}. TotalLength={TotalLength} PartialCount={PartialCount}. Destination commit will run in background.",
            fileTransferId,
            totalLength,
            normalizedPartialFileIds.Length);

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

        return string.Empty;
    }

    public Task<ITusFile> GetFileAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        throw new TusStoreException($"No TUS file found for file id {fileId}");
    }

    public async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        await storageResolver.DeleteStagingBlobAsync(fileId, cancellationToken);

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

    public Task<bool> VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken)
    {
        ResolveStoreFileId(fileId);
        return Task.FromResult(false);
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
        if (uploadStateRegistry.TryGet(fileId, out var existingState))
        {
            var cachedProgress = await uploadProgressCache.GetAsync(fileId, cancellationToken);
            if (cachedProgress is not null)
            {
                SyncStateFromProgress(existingState!, cachedProgress);
            }

            return existingState!;
        }

        var progress = await uploadProgressCache.GetAsync(fileId, cancellationToken);
        if (progress is not null)
        {
            return uploadStateRegistry.GetOrAdd(fileId, () => CreateStateFromSnapshot(progress));
        }

        var uploadLength = await GetCachedUploadLengthAsync(fileId, cancellationToken);
        var currentOffset = await GetDurableUploadOffsetAsync(fileId, cancellationToken);
        return uploadStateRegistry.GetOrAdd(
            fileId,
            () => new TusUploadState(uploadLength, currentOffset, _maxParallelBlockUploads));
    }

    private async Task<long> GetDurableUploadOffsetAsync(
        string fileId,
        CancellationToken cancellationToken,
        TusUploadDebugTiming? timing = null)
    {
        var cachedProgress = await uploadProgressCache.GetAsync(fileId, cancellationToken);
        if (cachedProgress is not null)
        {
            timing?.Step("durable.redisProgress", cachedProgress.AcceptedOffset);
            return cachedProgress.AcceptedOffset;
        }

        timing?.Step("durable.redisProgressMiss");

        var committedOffset = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        timing?.Step("durable.committedStagingLength", committedOffset);
        if (committedOffset > 0)
        {
            await TryRestorePartialRegistrationFromStagedUploadAsync(fileId, committedOffset, cancellationToken);
            return committedOffset;
        }

        var stagedSnapshot = await storageResolver.TryGetStagedBlocksSnapshotAsync(fileId, cancellationToken);
        if (stagedSnapshot is not null)
        {
            timing?.Step("durable.stagedBlocksSnapshot", stagedSnapshot.TotalLength);
            await TryRestorePartialRegistrationFromStagedUploadAsync(fileId, stagedSnapshot.TotalLength, cancellationToken);
            return stagedSnapshot.TotalLength;
        }

        var destinationLength = (await storageResolver.GetCommittedStripesAsync(fileId, cancellationToken)).TotalLength;
        if (destinationLength > 0)
        {
            timing?.Step("durable.destinationLength", destinationLength);
            return destinationLength;
        }

        timing?.Step("durable.zero");
        return 0;
    }

    private TusUploadState RegisterUploadState(string fileId, long uploadLength, long initialOffset)
        => uploadStateRegistry.GetOrAdd(fileId, () => new TusUploadState(uploadLength, initialOffset, _maxParallelBlockUploads));

    private TusUploadState CreateStateFromSnapshot(TusUploadProgressSnapshot snapshot)
    {
        var state = new TusUploadState(snapshot.UploadLength, snapshot.CommittedOffset, _maxParallelBlockUploads);
        state.AcceptedOffset = snapshot.AcceptedOffset;
        state.CommittedOffset = snapshot.CommittedOffset;
        state.NextBlockIndex = snapshot.NextBlockIndex;
        return state;
    }

    private static void SyncStateFromProgress(TusUploadState state, TusUploadProgressSnapshot progress)
    {
        lock (state.SyncRoot)
        {
            state.AcceptedOffset = progress.AcceptedOffset;
            state.CommittedOffset = progress.CommittedOffset;
            state.NextBlockIndex = progress.NextBlockIndex;
        }
    }

    private async Task RunProgressSideEffectsAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
    {
        await RefreshPartialRegistryAsync(fileId, uploadLength, cancellationToken);
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
        if (httpContext is null
            || !TusRouteHelper.IsPartialUploadPath(TusRouteHelper.GetRequestPath(httpContext))
            || !TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var fileTransferId))
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
        string? destinationFileId,
        int stripeIndex,
        TusUploadState state,
        string blockId,
        byte[] chunk,
        int sourceOffset,
        int length)
    {
        await state.ConcurrentUploader.WaitAsync();
        try
        {
            await using var chunkStream = new MemoryStream(chunk, sourceOffset, length, writable: false);
            if (destinationFileId is not null)
            {
                await storageResolver.StageTusBlockOnDestinationAsync(
                    destinationFileId,
                    stripeIndex,
                    blockId,
                    chunkStream,
                    CancellationToken.None);
            }
            else
            {
                await storageResolver.StageTusBlockAsync(fileId, blockId, chunkStream, CancellationToken.None);
            }
            lock (state.SyncRoot)
            {
                state.CommittedOffset += length;
                state.PendingUploads--;
                var previousProgress = state.ProgressSignal;
                state.ProgressSignal = NewProgressSignal();
                previousProgress.TrySetResult(state.CommittedOffset);
            }

            await uploadProgressCache.IncrementCommittedOffsetAsync(fileId, length, CancellationToken.None);
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

    private async Task<long> GetDurableStagingOffsetAsync(string fileId, CancellationToken cancellationToken)
    {
        if (await partialUploadRegistry.IsPartialAsync(fileId, cancellationToken))
        {
            var progress = await uploadProgressCache.GetAsync(fileId, cancellationToken);
            return progress?.CommittedOffset ?? 0;
        }

        var committedLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        var stagedLength = await storageResolver.GetStagedBlocksLengthAsync(fileId, cancellationToken);
        return committedLength + stagedLength;
    }

    private async Task WaitForDurableCommittedOffsetAsync(
        string fileId,
        long uploadLength,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddHours(2);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var progress = await uploadProgressCache.GetAsync(fileId, cancellationToken);
            if (progress is not null && progress.CommittedOffset >= uploadLength)
            {
                return;
            }

            var committedStagingLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
            if (committedStagingLength >= uploadLength)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        throw new TusStoreException(
            $"Timed out waiting for TUS blocks to stage for file id {fileId}. Expected {uploadLength} bytes.");
    }

    public async Task<bool> IsReadyForStagingFinalizeAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        var uploadLength = await GetUploadLengthAsync(fileId, cancellationToken);
        if (uploadLength is null or <= 0)
        {
            return false;
        }

        var progress = await uploadProgressCache.GetAsync(fileId, cancellationToken);
        if (progress is null || progress.AcceptedOffset < uploadLength.Value)
        {
            return false;
        }

        var committedLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        return committedLength < uploadLength.Value;
    }

    public async Task<bool> IsReadyForTransferCompletionAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        if (await partialUploadRegistry.IsPartialAsync(fileId, cancellationToken))
        {
            return false;
        }

        if (await GetUploadConcatAsync(fileId, cancellationToken) is FileConcatPartial)
        {
            return false;
        }

        var uploadLength = await GetUploadLengthAsync(fileId, cancellationToken);
        if (uploadLength is null or <= 0)
        {
            return false;
        }

        var partialIds = await partialUploadRegistry.TryGetFinalConcatPartialIdsAsync(fileId, cancellationToken);
        if (partialIds is { Length: > 0 })
        {
            return false;
        }

        var concatStatus = await partialUploadRegistry.TryGetConcatStatusAsync(fileId, cancellationToken);
        if (concatStatus is TusConcatStatus.Pending or TusConcatStatus.InProgress)
        {
            return false;
        }

        var progress = await uploadProgressCache.GetAsync(fileId, cancellationToken);
        if (progress is not null)
        {
            return progress.AcceptedOffset >= uploadLength.Value;
        }

        var committedLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        return committedLength >= uploadLength.Value;
    }

    public async Task EnsureFinalConcatenatedAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        while (true)
        {
            var result = await ProcessConcatChainStepAsync(fileId, cancellationToken);
            if (result.ChainComplete)
            {
                return;
            }

            if (!result.StepCompleted)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    public async Task<TusConcatChainStepResult> ProcessConcatChainStepAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        var partialFileIds = await partialUploadRegistry.TryGetFinalConcatPartialIdsAsync(fileId, cancellationToken);
        if (partialFileIds is null or { Length: 0 })
        {
            if (await TryPromoteConcatCompleteFromStagingAsync(fileId, cancellationToken))
            {
                return new TusConcatChainStepResult(StepCompleted: true, ChainComplete: true, ShouldRetryStep: false);
            }

            var concatStatus = await partialUploadRegistry.TryGetConcatStatusAsync(fileId, cancellationToken);
            if (concatStatus is TusConcatStatus.Pending or TusConcatStatus.InProgress)
            {
                var expectedUploadLength = await GetCachedUploadLengthAsync(fileId, cancellationToken);
                var committedLength = (await storageResolver.GetCommittedStripesAsync(fileId, cancellationToken)).TotalLength;
                throw new TusStoreException(
                    $"TUS concatenation cannot resume for file id {fileId}: partial references are missing and destination is incomplete ({committedLength}/{expectedUploadLength}).");
            }

            return new TusConcatChainStepResult(StepCompleted: true, ChainComplete: true, ShouldRetryStep: false);
        }

        var uploadLength = await GetCachedUploadLengthAsync(fileId, cancellationToken);
        var destinationCommittedLength = (await storageResolver.GetCommittedStripesAsync(fileId, cancellationToken)).TotalLength;
        if (await partialUploadRegistry.TryGetConcatStatusAsync(fileId, cancellationToken) == TusConcatStatus.Complete
            && destinationCommittedLength >= uploadLength)
        {
            await concatCheckpointStore.ClearCheckpointAsync(fileId, cancellationToken);
            return new TusConcatChainStepResult(StepCompleted: true, ChainComplete: true, ShouldRetryStep: false);
        }

        var checkpoint = await concatCheckpointStore.TryGetCheckpointAsync(fileId, cancellationToken)
            ?? new TusConcatCheckpoint();

        switch (checkpoint.NextStep)
        {
            case TusConcatChainStep.ValidatePartials:
                return await ProcessValidatePartialsStepAsync(
                    fileId,
                    partialFileIds,
                    uploadLength,
                    checkpoint,
                    cancellationToken);

            case TusConcatChainStep.PrepareCommit:
                return await ProcessPrepareCommitStepAsync(
                    fileId,
                    partialFileIds,
                    uploadLength,
                    checkpoint,
                    cancellationToken);

            case TusConcatChainStep.CommitDestination:
                return await ProcessCommitDestinationStepAsync(
                    fileId,
                    uploadLength,
                    checkpoint,
                    cancellationToken);

            case TusConcatChainStep.Cleanup:
                return await ProcessCleanupStepAsync(fileId, partialFileIds, checkpoint, cancellationToken);

            default:
                throw new TusStoreException($"Unknown TUS concat chain step {checkpoint.NextStep} for file id {fileId}.");
        }
    }

    private async Task<TusConcatChainStepResult> ProcessValidatePartialsStepAsync(
        string fileId,
        string[] partialFileIds,
        long uploadLength,
        TusConcatCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        if (checkpoint.ValidatedPartialCount >= partialFileIds.Length)
        {
            var advanced = checkpoint with { NextStep = TusConcatChainStep.PrepareCommit };
            await concatCheckpointStore.SaveCheckpointAsync(fileId, advanced, cancellationToken);
            return new TusConcatChainStepResult(
                StepCompleted: true,
                ChainComplete: false,
                ShouldRetryStep: false,
                NextStep: TusConcatChainStep.PrepareCommit);
        }

        var partialFileId = partialFileIds[checkpoint.ValidatedPartialCount];
        var partialInfo = await partialUploadRegistry.TryGetPartialInfoAsync(partialFileId, cancellationToken);
        if (partialInfo is null)
        {
            throw new TusStoreException($"Unknown partial upload id {partialFileId}.");
        }

        var progress = await uploadProgressCache.GetAsync(partialFileId, cancellationToken);
        if (progress is null || progress.CommittedOffset < partialInfo.Value.UploadLength)
        {
            logger.LogInformation(
                "TUS concat chain waiting for partial {PartialFileId} ({CommittedOffset}/{UploadLength}) on file id {FileId}.",
                partialFileId,
                progress?.CommittedOffset ?? 0,
                partialInfo.Value.UploadLength,
                fileId);
            return new TusConcatChainStepResult(
                StepCompleted: false,
                ChainComplete: false,
                ShouldRetryStep: true,
                NextStep: TusConcatChainStep.ValidatePartials);
        }

        var nextCheckpoint = checkpoint with
        {
            ValidatedPartialCount = checkpoint.ValidatedPartialCount + 1,
            TotalValidatedLength = checkpoint.TotalValidatedLength + partialInfo.Value.UploadLength,
            NextStep = checkpoint.ValidatedPartialCount + 1 >= partialFileIds.Length
                ? TusConcatChainStep.PrepareCommit
                : TusConcatChainStep.ValidatePartials
        };
        await concatCheckpointStore.SaveCheckpointAsync(fileId, nextCheckpoint, cancellationToken);

        logger.LogInformation(
            "TUS concat chain validated partial {PartialIndex}/{PartialCount} for file id {FileId}.",
            nextCheckpoint.ValidatedPartialCount,
            partialFileIds.Length,
            fileId);

        if (nextCheckpoint.NextStep == TusConcatChainStep.PrepareCommit
            && nextCheckpoint.TotalValidatedLength != uploadLength)
        {
            throw new TusStoreException(
                $"Concatenated upload length mismatch for file id {fileId}. Expected {uploadLength}, partial sum {nextCheckpoint.TotalValidatedLength}.");
        }

        return new TusConcatChainStepResult(
            StepCompleted: true,
            ChainComplete: false,
            ShouldRetryStep: false,
            NextStep: nextCheckpoint.NextStep);
    }

    private async Task<TusConcatChainStepResult> ProcessPrepareCommitStepAsync(
        string fileId,
        string[] partialFileIds,
        long uploadLength,
        TusConcatCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        if (checkpoint.TotalValidatedLength != uploadLength)
        {
            throw new TusStoreException(
                $"Concatenated upload length mismatch for file id {fileId}. Expected {uploadLength}, partial sum {checkpoint.TotalValidatedLength}.");
        }

        var layout = await GetConcatLayoutAsync(fileId, uploadLength, checkpoint, cancellationToken);
        var stripeIndex = checkpoint.PreparedStripeCount;
        var expectedStripeLength = layout.LengthOfStripe(stripeIndex);

        var stagedSnapshot = await storageResolver.TryGetStripeStagedBlocksSnapshotAsync(fileId, stripeIndex, cancellationToken);
        if (stagedSnapshot is not { BlockIds.Count: > 0 })
        {
            throw new TusStoreException(
                $"Cannot commit TUS concatenation for file id {fileId} because no blocks were staged on stripe {stripeIndex}.");
        }

        if (stagedSnapshot.TotalLength != expectedStripeLength)
        {
            throw new TusStoreException(
                $"Cannot commit TUS concatenation for file id {fileId}. Stripe {stripeIndex} staged " +
                $"{stagedSnapshot.TotalLength} bytes, expected {expectedStripeLength}.");
        }

        if (stagedSnapshot.BlockIds.Count > _maxBlocksPerStripe)
        {
            throw new TusStoreException(
                $"Cannot commit TUS concatenation for file id {fileId}. Stripe {stripeIndex} block count " +
                $"{stagedSnapshot.BlockIds.Count} exceeds the limit of {_maxBlocksPerStripe}.");
        }

        var preparedStripeCount = stripeIndex + 1;
        var allStripesPrepared = preparedStripeCount >= layout.StripeCount;
        var nextCheckpoint = checkpoint with
        {
            BlockCount = checkpoint.BlockCount + stagedSnapshot.BlockIds.Count,
            StagedLength = checkpoint.StagedLength + stagedSnapshot.TotalLength,
            PreparedStripeCount = preparedStripeCount,
            StripeSizeBytes = layout.StripeSize,
            StripeCount = layout.StripeCount,
            NextStep = allStripesPrepared ? TusConcatChainStep.CommitDestination : TusConcatChainStep.PrepareCommit
        };

        if (allStripesPrepared && nextCheckpoint.StagedLength != uploadLength)
        {
            throw new TusStoreException(
                $"Cannot commit TUS concatenation for file id {fileId}. Staged {nextCheckpoint.StagedLength} bytes, expected {uploadLength}.");
        }

        await concatCheckpointStore.SaveCheckpointAsync(fileId, nextCheckpoint, cancellationToken);

        logger.LogInformation(
            "TUS concat chain prepared stripe {StripeIndex}/{StripeCount} for file id {FileId}. BlockCount={BlockCount} StagedBytes={StagedBytes} PartialCount={PartialCount}",
            preparedStripeCount,
            layout.StripeCount,
            fileId,
            stagedSnapshot.BlockIds.Count,
            stagedSnapshot.TotalLength,
            partialFileIds.Length);

        return new TusConcatChainStepResult(
            StepCompleted: true,
            ChainComplete: false,
            ShouldRetryStep: false,
            NextStep: nextCheckpoint.NextStep);
    }

    private async Task<TusConcatChainStepResult> ProcessCommitDestinationStepAsync(
        string fileId,
        long uploadLength,
        TusConcatCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        var layout = await GetConcatLayoutAsync(fileId, uploadLength, checkpoint, cancellationToken);
        var stripeIndex = checkpoint.CommittedStripeCount;
        if (stripeIndex >= layout.StripeCount)
        {
            return await AdvanceToCleanupAsync(fileId, checkpoint, cancellationToken);
        }

        var expectedStripeLength = layout.LengthOfStripe(stripeIndex);

        var committedLength = await storageResolver.GetStripeBlobLengthAsync(fileId, stripeIndex, cancellationToken);
        if (committedLength >= expectedStripeLength)
        {
            return await AdvanceStripeAsync(fileId, checkpoint, stripeIndex, layout, cancellationToken);
        }

        var stagedSnapshot = await storageResolver.TryGetStripeStagedBlocksSnapshotAsync(fileId, stripeIndex, cancellationToken);
        if (stagedSnapshot is not { BlockIds.Count: > 0 })
        {
            throw new TusStoreException(
                $"Cannot commit TUS concatenation for file id {fileId} because no blocks were staged on stripe {stripeIndex}.");
        }

        var metadata = stripeIndex == 0
            ? new Dictionary<string, string>
            {
                [AzureStorageConstants.TusUploadLengthMetadataKey] = uploadLength.ToString(),
                [AzureStorageConstants.TusStripeSizeMetadataKey] = layout.StripeSize.ToString(),
                [AzureStorageConstants.TusStripeCountMetadataKey] = layout.StripeCount.ToString()
            }
            : null;

        logger.LogInformation(
            "TUS concat chain committing stripe {StripeIndex}/{StripeCount} for file id {FileId}. BlockCount={BlockCount} StagedBytes={StagedBytes} UploadLength={UploadLength}",
            stripeIndex,
            layout.StripeCount,
            fileId,
            stagedSnapshot.BlockIds.Count,
            stagedSnapshot.TotalLength,
            uploadLength);

        await storageResolver.CommitStripeBlocksAsync(fileId, stripeIndex, stagedSnapshot.BlockIds, metadata, cancellationToken);

        return await AdvanceStripeAsync(fileId, checkpoint, stripeIndex, layout, cancellationToken);
    }

    private async Task<TusConcatChainStepResult> AdvanceStripeAsync(
        string fileId,
        TusConcatCheckpoint checkpoint,
        int stripeIndex,
        StripeLayout layout,
        CancellationToken cancellationToken)
    {
        var committedStripeCount = stripeIndex + 1;
        var allCommitted = committedStripeCount >= layout.StripeCount;
        var nextCheckpoint = checkpoint with
        {
            CommittedStripeCount = committedStripeCount,
            StripeSizeBytes = layout.StripeSize,
            StripeCount = layout.StripeCount,
            NextStep = allCommitted ? TusConcatChainStep.Cleanup : TusConcatChainStep.CommitDestination
        };
        await concatCheckpointStore.SaveCheckpointAsync(fileId, nextCheckpoint, cancellationToken);

        return new TusConcatChainStepResult(
            StepCompleted: true,
            ChainComplete: false,
            ShouldRetryStep: false,
            NextStep: nextCheckpoint.NextStep);
    }

    private async Task<TusConcatChainStepResult> AdvanceToCleanupAsync(
        string fileId,
        TusConcatCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        await concatCheckpointStore.SaveCheckpointAsync(
            fileId,
            checkpoint with { NextStep = TusConcatChainStep.Cleanup },
            cancellationToken);
        return new TusConcatChainStepResult(
            StepCompleted: true,
            ChainComplete: false,
            ShouldRetryStep: false,
            NextStep: TusConcatChainStep.Cleanup);
    }

    /// <summary>
    /// The layout recorded on the checkpoint when the concatenation was accepted, so a configuration
    /// change part way through an upload cannot relayout it.
    /// </summary>
    private async Task<StripeLayout> GetConcatLayoutAsync(
        string fileId,
        long uploadLength,
        TusConcatCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        var stripeSize = checkpoint.StripeSizeBytes > 0
            ? checkpoint.StripeSizeBytes
            : await storageResolver.GetStripeSizeAsync(fileId, cancellationToken);
        return new StripeLayout(uploadLength, stripeSize);
    }

    private async Task<TusConcatChainStepResult> ProcessCleanupStepAsync(
        string fileId,
        string[] partialFileIds,
        TusConcatCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        await partialUploadRegistry.MarkConcatCompleteAsync(fileId, cancellationToken);
        await partialUploadRegistry.ClearFinalConcatPartialReferencesAsync(fileId, cancellationToken);
        await concatCheckpointStore.ClearCheckpointAsync(fileId, cancellationToken);
        if (Guid.TryParse(fileId, out var completedFileTransferId))
        {
            await partialUploadRegistry.ClearStripeBlockCountsAsync(completedFileTransferId, cancellationToken);
        }

        logger.LogInformation(
            "TUS concat chain completed cleanup for file id {FileId}. TotalLength={TotalLength} PartialCount={PartialCount}",
            fileId,
            checkpoint.StagedLength,
            partialFileIds.Length);

        foreach (var partialFileId in partialFileIds)
        {
            await partialUploadRegistry.RemovePartialAsync(partialFileId, cancellationToken);
            await uploadProgressCache.RemoveAsync(partialFileId, cancellationToken);
            uploadStateRegistry.Remove(partialFileId);
            _uploadLengths.TryRemove(partialFileId, out _);
        }

        return new TusConcatChainStepResult(StepCompleted: true, ChainComplete: true, ShouldRetryStep: false);
    }

    public async Task<bool> TryPromoteConcatCompleteFromStagingAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        if (await partialUploadRegistry.TryGetConcatStatusAsync(fileId, cancellationToken) == TusConcatStatus.Complete)
        {
            var uploadLength = await GetCachedUploadLengthAsync(fileId, cancellationToken);
            var committedLength = (await storageResolver.GetCommittedStripesAsync(fileId, cancellationToken)).TotalLength;
            if (committedLength >= uploadLength)
            {
                return true;
            }

            throw new TusStoreException(
                $"TUS concatenation is marked complete for file id {fileId}, but destination blob is missing or incomplete.");
        }

        var expectedLength = await GetCachedUploadLengthAsync(fileId, cancellationToken);
        if (expectedLength <= 0)
        {
            return false;
        }

        var destinationLength = (await storageResolver.GetCommittedStripesAsync(fileId, cancellationToken)).TotalLength;
        if (destinationLength < expectedLength)
        {
            return false;
        }

        await partialUploadRegistry.MarkConcatCompleteAsync(fileId, cancellationToken);
        await partialUploadRegistry.ClearFinalConcatPartialReferencesAsync(fileId, cancellationToken);

        logger.LogInformation(
            "TUS concatenation promoted to complete from existing destination blob for file id {FileId}. DestinationLength={DestinationLength}",
            fileId,
            destinationLength);
        return true;
    }

    public async Task<bool> IsStagingBlobCommittedAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        var uploadLength = await GetCachedUploadLengthAsync(fileId, cancellationToken);
        if (uploadLength <= 0)
        {
            return false;
        }

        var partialFileIds = await partialUploadRegistry.TryGetFinalConcatPartialIdsAsync(fileId, cancellationToken);
        var concatStatus = await partialUploadRegistry.TryGetConcatStatusAsync(fileId, cancellationToken);
        if (partialFileIds is { Length: > 0 } || concatStatus == TusConcatStatus.Complete)
        {
            var destinationLength = (await storageResolver.GetCommittedStripesAsync(fileId, cancellationToken)).TotalLength;
            return destinationLength >= uploadLength;
        }

        var committedLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        return committedLength >= uploadLength;
    }

    private async Task EnsurePartialReadyForConcatenationAsync(
        string partialFileId,
        long partialLength,
        CancellationToken cancellationToken)
    {
        var progress = await uploadProgressCache.GetAsync(partialFileId, cancellationToken);
        if (progress is { CommittedOffset: var committedOffset } && committedOffset >= partialLength)
        {
            return;
        }

        throw new TusStoreException(
            $"Partial upload {partialFileId} is not ready for concatenation. Expected {partialLength} bytes, committed {progress?.CommittedOffset ?? 0}.");
    }

    public async Task FinalizeStagingFromDurableStateAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = ResolveStoreFileId(fileId);
        var uploadLength = await GetCachedUploadLengthAsync(fileId, cancellationToken);

        if (await partialUploadRegistry.TryGetConcatStatusAsync(fileId, cancellationToken) == TusConcatStatus.Complete)
        {
            var concatCommittedLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
            if (concatCommittedLength >= uploadLength)
            {
                logger.LogInformation(
                    "TUS staging already committed for concat-complete file id {FileId}. CommittedLength={CommittedLength} UploadLength={UploadLength}",
                    fileId,
                    concatCommittedLength,
                    uploadLength);
                await storageResolver.SetStagingUploadLengthAsync(fileId, uploadLength, cancellationToken);
                return;
            }

            throw new TusStoreException(
                $"TUS concat is marked complete for file id {fileId}, but staging blob is missing or incomplete ({concatCommittedLength}/{uploadLength}).");
        }

        await WaitForDurableCommittedOffsetAsync(fileId, uploadLength, cancellationToken);

        var committedLength = await storageResolver.GetCommittedStagingLengthAsync(fileId, cancellationToken);
        if (committedLength >= uploadLength)
        {
            logger.LogInformation(
                "TUS staging already committed for file id {FileId}. CommittedLength={CommittedLength} UploadLength={UploadLength}",
                fileId,
                committedLength,
                uploadLength);
            await storageResolver.SetStagingUploadLengthAsync(fileId, uploadLength, cancellationToken);
            return;
        }

        var stagedSnapshot = await storageResolver.TryGetStagedBlocksSnapshotAsync(fileId, cancellationToken);
        if (stagedSnapshot is not { BlockIds.Count: > 0 })
        {
            throw new TusStoreException($"Cannot finalize TUS upload for file id {fileId} because no blocks were staged.");
        }

        if (stagedSnapshot.TotalLength != uploadLength)
        {
            throw new TusStoreException(
                $"Cannot finalize TUS upload for file id {fileId}. Staged {stagedSnapshot.TotalLength} bytes, expected {uploadLength}.");
        }

        logger.LogInformation(
            "TUS committing staged blocks for file id {FileId}. BlockCount={BlockCount} StagedBytes={StagedBytes} UploadLength={UploadLength}",
            fileId,
            stagedSnapshot.BlockIds.Count,
            stagedSnapshot.TotalLength,
            uploadLength);

        await storageResolver.CommitTusBlocksAsync(fileId, stagedSnapshot.BlockIds, cancellationToken);
        await storageResolver.SetStagingUploadLengthAsync(fileId, uploadLength, cancellationToken);
    }

    public Task CleanupCompletedUploadAsync(string fileId, CancellationToken cancellationToken)
    {
        var resolvedFileId = ResolveStoreFileId(fileId);
        logger.LogInformation("TUS cleaning up completed upload state for file id {FileId}", resolvedFileId);
        return CleanupUploadState(resolvedFileId, cancellationToken);
    }

    private async Task CleanupUploadState(string fileId, CancellationToken cancellationToken)
    {
        uploadStateRegistry.Remove(fileId);

        if (await partialUploadRegistry.IsPartialAsync(fileId, cancellationToken))
        {
            return;
        }

        await uploadProgressCache.RemoveAsync(fileId, cancellationToken);
        _uploadLengths.TryRemove(fileId, out _);
        await partialUploadRegistry.RemoveFinalConcatAsync(fileId, cancellationToken);
        await partialUploadRegistry.RemoveUploadAsync(fileId, cancellationToken);
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

    /// <summary>
    /// PATCH responses must expose accepted offset so clients can pipeline chunks.
    /// HEAD/resume must expose committed (durable) offset only.
    /// </summary>
    private bool IsHeadOffsetRequest()
    {
        var httpContext = httpContextAccessor.HttpContext;
        return httpContext is not null && HttpMethods.IsHead(httpContext.Request.Method);
    }

    private static string DescribeUploadConcat(FileConcat? uploadConcat)
        => uploadConcat switch
        {
            null => "none",
            FileConcatPartial => "partial",
            FileConcatFinal => "final",
            _ => uploadConcat.GetType().Name
        };
}
