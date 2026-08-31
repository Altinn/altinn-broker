using System.Collections.Concurrent;
using System.Security.Cryptography;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Integrations.Azure;

using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Tus;

public sealed record TusStagedBlocksSnapshot(
    long TotalLength,
    IReadOnlyList<string> BlockIds,
    long NextBlockIndex);

public interface ITusStorageResolver
{
    Task<long> StageTusBlockAsync(string fileId, string blockId, Stream blockData, CancellationToken cancellationToken);
    Task<long> StageTusBlockOnDestinationAsync(string fileId, int stripeIndex, string blockId, Stream blockData, CancellationToken cancellationToken);
    Task CommitStripeBlocksAsync(
        string fileId,
        int stripeIndex,
        IReadOnlyList<string> blockIds,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken);
    Task<TusStagedBlocksSnapshot?> TryGetStripeStagedBlocksSnapshotAsync(string fileId, int stripeIndex, CancellationToken cancellationToken);
    Task<long> GetStripeBlobLengthAsync(string fileId, int stripeIndex, CancellationToken cancellationToken);
    Task<CommittedStripes> GetCommittedStripesAsync(string fileId, CancellationToken cancellationToken);
    Task DeleteAllStripesAsync(string fileId, CancellationToken cancellationToken);
    Task<long> GetStripeSizeAsync(string fileId, CancellationToken cancellationToken);
    Task CommitTusBlocksAsync(
        string fileId,
        IReadOnlyList<string> blockIds,
        CancellationToken cancellationToken);
    Task<byte[]> ComputeCommittedStagingMd5Async(string fileId, CancellationToken cancellationToken);
    Task SetCommittedStagingMd5Async(string fileId, byte[] md5Hash, CancellationToken cancellationToken);
    Task<bool> StagingBlobExistsAsync(string fileId, CancellationToken cancellationToken);
    Task<bool> HasStagedBlocksAsync(string fileId, CancellationToken cancellationToken);
    Task<long> GetCommittedStagingLengthAsync(string fileId, CancellationToken cancellationToken);
    Task<long> GetStagedBlocksLengthAsync(string fileId, CancellationToken cancellationToken);
    Task<TusStagedBlocksSnapshot?> TryGetStagedBlocksSnapshotAsync(string fileId, CancellationToken cancellationToken);
    Task<long> GetDestinationUncommittedBlocksLengthAsync(string fileId, int stripeIndex, CancellationToken cancellationToken);
    Task<long?> TryGetStagingUploadLengthAsync(string fileId, CancellationToken cancellationToken);
    Task InitializePartialStagingBlobAsync(string fileId, long uploadLength, CancellationToken cancellationToken);
    Task SetStagingUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken);
    Task SetDestinationUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken);
    Task<long> ConcatenatePartialStagingBlobsAsync(
        string finalFileId,
        IReadOnlyList<string> partialFileIds,
        CancellationToken cancellationToken);
    Task DeleteStagingBlobAsync(string fileId, CancellationToken cancellationToken);
}

public class TusStorageResolver(
    IFileTransferRepository fileTransferRepository,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IHostEnvironment hostEnvironment,
    ITusPartialUploadRegistry partialUploadRegistry,
    IHttpContextAccessor httpContextAccessor,
    ILogger<TusStorageResolver> logger) : ITusStorageResolver
{
    private const long MaxPutBlockFromUrlSize = 100L * 1024 * 1024;

    private readonly ConcurrentDictionary<Guid, TusStorageContext> _storageContextsByFileTransferId = new();
    private readonly ConcurrentDictionary<string, BlobContainerClient> _containerClients = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BlockBlobClient> _blockBlobClients = new(StringComparer.OrdinalIgnoreCase);
    private readonly DefaultAzureCredential _azureCredential = new();

    public async Task<long> StageTusBlockAsync(
        string fileId,
        string blockId,
        Stream blockData,
        CancellationToken cancellationToken)
    {
        if (!blockData.CanSeek)
        {
            throw new ArgumentException("Block data stream must be seekable.", nameof(blockData));
        }

        var blockBytes = blockData.Length - blockData.Position;
        int? expectedBytes = blockBytes is >= 0 and <= int.MaxValue ? (int)blockBytes : null;
        using var timing = TusUploadDebugTiming.Start(logger, "StageTusBlock", fileId, expectedBytes);

        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken, timing);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        timing.Step("getBlockBlobClient");
        var blockBlobClient = GetBlockBlobClient(storageContext, fileId);
        await blockBlobClient.StageBlockAsync(
            blockId,
            blockData,
            cancellationToken: cancellationToken);
        timing.Step("azure.stageBlock", blockBytes);
        return blockBytes;
    }

    public async Task<long> StageTusBlockOnDestinationAsync(
        string fileId,
        int stripeIndex,
        string blockId,
        Stream blockData,
        CancellationToken cancellationToken)
    {
        if (!blockData.CanSeek)
        {
            throw new ArgumentException("Block data stream must be seekable.", nameof(blockData));
        }

        var blockBytes = blockData.Length - blockData.Position;
        int? expectedBytes = blockBytes is >= 0 and <= int.MaxValue ? (int)blockBytes : null;
        using var timing = TusUploadDebugTiming.Start(logger, "StageTusBlockDestination", fileId, expectedBytes);

        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken, timing);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        timing.Step("getDestinationBlockBlobClient");
        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = TusConcatDestinationStorage.GetDestinationBlockBlobClient(containerClient, Guid.Parse(fileId), stripeIndex);
        await blockBlobClient.StageBlockAsync(
            blockId,
            blockData,
            cancellationToken: cancellationToken);
        timing.Step("azure.stageBlock.destination", blockBytes);
        return blockBytes;
    }

    public async Task CommitStripeBlocksAsync(
        string fileId,
        int stripeIndex,
        IReadOnlyList<string> blockIds,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = TusConcatDestinationStorage.GetDestinationBlockBlobClient(containerClient, Guid.Parse(fileId), stripeIndex);
        var options = new CommitBlockListOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/octet-stream"
            }
        };
        if (metadata is not null)
        {
            options.Metadata = metadata.ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        await blockBlobClient.CommitBlockListAsync(blockIds, options, cancellationToken: cancellationToken);
    }

    public async Task<long> GetStripeBlobLengthAsync(string fileId, int stripeIndex, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return 0;
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blobClient = containerClient.GetBlobClient(StripeLayout.GetStripeBlobName(Guid.Parse(fileId), stripeIndex));
        try
        {
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.ContentLength;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return 0;
        }
    }

    public async Task<CommittedStripes> GetCommittedStripesAsync(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null || !Guid.TryParse(fileId, out var fileTransferId))
        {
            return new CommittedStripes(0, 0, []);
        }

        return await CommittedStripes.ReadAsync(GetBlobContainerClient(storageContext), fileTransferId, cancellationToken);
    }

    public async Task<long> GetStripeSizeAsync(string fileId, CancellationToken cancellationToken)
        => (await ResolveStorageContextAsync(fileId, cancellationToken))?.StripeSizeBytes ?? 0;

    public async Task DeleteAllStripesAsync(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null || !Guid.TryParse(fileId, out var fileTransferId))
        {
            return;
        }

        var containerClient = GetBlobContainerClient(storageContext);
        await foreach (var blob in containerClient.GetBlobsAsync(
            BlobTraits.None,
            BlobStates.None,
            prefix: StripeLayout.GetStripeBlobPrefix(fileTransferId),
            cancellationToken))
        {
            await containerClient.DeleteBlobIfExistsAsync(blob.Name, cancellationToken: cancellationToken);
        }
    }

    public async Task CommitTusBlocksAsync(
        string fileId,
        IReadOnlyList<string> blockIds,
        CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        await blockBlobClient.CommitBlockListAsync(
            blockIds,
            new CommitBlockListOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/octet-stream"
                }
            },
            cancellationToken: cancellationToken);
    }

    public async Task<byte[]> ComputeCommittedStagingMd5Async(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await using var contentStream = await blockBlobClient.OpenReadAsync(cancellationToken: cancellationToken);
                return await MD5.HashDataAsync(contentStream, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 412 && attempt < 2)
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        throw new InvalidOperationException($"Failed to read committed staging blob for file id {fileId}.");
    }

    public async Task SetCommittedStagingMd5Async(string fileId, byte[] md5Hash, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        var properties = await blockBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var metadata = properties.Value.Metadata.ToDictionary(static k => k.Key, static v => v.Value);
        metadata[AzureStorageConstants.TusMd5ChecksumMetadataKey] = Convert.ToBase64String(md5Hash);
        await blockBlobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
        await blockBlobClient.SetHttpHeadersAsync(
            new BlobHttpHeaders
            {
                ContentType = "application/octet-stream",
                ContentHash = md5Hash
            },
            cancellationToken: cancellationToken);
    }

    public async Task<bool> StagingBlobExistsAsync(string fileId, CancellationToken cancellationToken)
    {
        var blockBlobClient = await GetBlockStagingBlobClientAsync(fileId, cancellationToken);
        if (blockBlobClient is null)
        {
            return false;
        }

        if (await blockBlobClient.ExistsAsync(cancellationToken))
        {
            return true;
        }

        var uncommittedBlocks = await TryGetUncommittedBlocksAsync(blockBlobClient, cancellationToken);
        return uncommittedBlocks is { Count: > 0 };
    }

    public async Task<bool> HasStagedBlocksAsync(string fileId, CancellationToken cancellationToken)
    {
        var blockBlobClient = await GetBlockStagingBlobClientAsync(fileId, cancellationToken);
        if (blockBlobClient is null)
        {
            return false;
        }

        var uncommittedBlocks = await TryGetUncommittedBlocksAsync(blockBlobClient, cancellationToken);
        return uncommittedBlocks is { Count: > 0 };
    }

    public async Task<long> GetStagedBlocksLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        var blockBlobClient = await GetBlockStagingBlobClientAsync(fileId, cancellationToken);
        if (blockBlobClient is null)
        {
            return 0;
        }

        var uncommittedBlocks = await TryGetUncommittedBlocksAsync(blockBlobClient, cancellationToken);
        return uncommittedBlocks?.Sum(static block => block.SizeLong) ?? 0;
    }

    public async Task<TusStagedBlocksSnapshot?> TryGetStagedBlocksSnapshotAsync(
        string fileId,
        CancellationToken cancellationToken)
    {
        var blockBlobClient = await GetBlockStagingBlobClientAsync(fileId, cancellationToken);
        if (blockBlobClient is null)
        {
            return null;
        }

        var uncommittedBlocks = await TryGetUncommittedBlocksAsync(blockBlobClient, cancellationToken);
        if (uncommittedBlocks is null || uncommittedBlocks.Count == 0)
        {
            return null;
        }

        var orderedBlocks = uncommittedBlocks
            .Select(block => new { Block = block, Index = TusBlockIds.TryParseSortableIndex(block.Name) })
            .Where(entry => entry.Index.HasValue)
            .OrderBy(entry => entry.Index!.Value)
            .ToList();
        if (orderedBlocks.Count == 0)
        {
            return null;
        }

        var blockIds = orderedBlocks.Select(entry => entry.Block.Name).ToList();
        var totalLength = orderedBlocks.Sum(entry => entry.Block.SizeLong);
        var nextBlockIndex = orderedBlocks[^1].Index!.Value + 1;
        return new TusStagedBlocksSnapshot(totalLength, blockIds, nextBlockIndex);
    }

    public async Task<TusStagedBlocksSnapshot?> TryGetStripeStagedBlocksSnapshotAsync(
        string fileId,
        int stripeIndex,
        CancellationToken cancellationToken)
    {
        var blockBlobClient = await GetDestinationBlockBlobClientAsync(fileId, stripeIndex, cancellationToken);
        if (blockBlobClient is null)
        {
            return null;
        }

        return await TusConcatDestinationStorage.TryGetStagedBlocksSnapshotAsync(blockBlobClient, cancellationToken);
    }

    public async Task<long> GetDestinationUncommittedBlocksLengthAsync(string fileId, int stripeIndex, CancellationToken cancellationToken)
    {
        var blockBlobClient = await GetDestinationBlockBlobClientAsync(fileId, stripeIndex, cancellationToken);
        if (blockBlobClient is null)
        {
            return 0;
        }

        var uncommittedBlocks = await TusConcatDestinationStorage.TryGetUncommittedBlocksAsync(blockBlobClient, cancellationToken);
        return uncommittedBlocks?.Sum(static block => block.SizeLong) ?? 0;
    }

    private async Task<BlockBlobClient?> GetDestinationBlockBlobClientAsync(
        string fileId,
        int stripeIndex,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(fileId, out var fileTransferId))
        {
            return null;
        }

        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return null;
        }

        return TusConcatDestinationStorage.GetDestinationBlockBlobClient(
            GetBlobContainerClient(storageContext),
            fileTransferId,
            stripeIndex);
    }

    private async Task<BlockBlobClient?> GetBlockStagingBlobClientAsync(
        string fileId,
        CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return null;
        }

        return GetBlockBlobClient(storageContext, fileId);
    }

    /// <summary>
    /// Block blobs with only uncommitted staged blocks are not visible to ExistsAsync/GetProperties.
    /// Use GetBlockList(Uncommitted) to detect in-progress TUS uploads.
    /// </summary>
    private static async Task<IReadOnlyList<BlobBlock>?> TryGetUncommittedBlocksAsync(
        BlockBlobClient blockBlobClient,
        CancellationToken cancellationToken)
    {
        try
        {
            var blockList = await blockBlobClient.GetBlockListAsync(
                BlockListTypes.Uncommitted,
                cancellationToken: cancellationToken);
            return blockList.Value.UncommittedBlocks.ToList();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static long? TryParseBlockIndex(string blockId) => TusBlockIds.TryParseSortableIndex(blockId);

    public async Task SetDestinationUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
    {
        var blockBlobClient = await GetDestinationBlockBlobClientAsync(fileId, stripeIndex: 0, cancellationToken);
        if (blockBlobClient is null)
        {
            return;
        }

        await TusConcatDestinationStorage.SetUploadLengthMetadataAsync(blockBlobClient, uploadLength, cancellationToken);
    }

    public async Task<long?> TryGetStagingUploadLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        var blockBlobClient = await GetBlockStagingBlobClientAsync(fileId, cancellationToken);
        if (blockBlobClient is null)
        {
            return null;
        }

        try
        {
            var properties = await blockBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (properties.Value.Metadata.TryGetValue(AzureStorageConstants.TusUploadLengthMetadataKey, out var uploadLengthValue)
                && long.TryParse(uploadLengthValue, out var uploadLength))
            {
                return uploadLength;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }

        return null;
    }

    public async Task InitializePartialStagingBlobAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        var blockBlobClient = GetBlockBlobClient(storageContext, fileId);
        if (await blockBlobClient.ExistsAsync(cancellationToken))
        {
            await SetStagingUploadLengthAsync(fileId, uploadLength, cancellationToken);
            return;
        }

        await using var emptyStream = new MemoryStream();
        await blockBlobClient.UploadAsync(
            emptyStream,
            new BlobUploadOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    [AzureStorageConstants.TusUploadLengthMetadataKey] = uploadLength.ToString()
                },
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/octet-stream"
                }
            },
            cancellationToken);
    }

    public async Task SetStagingUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        if (!await blockBlobClient.ExistsAsync(cancellationToken))
        {
            return;
        }

        var properties = await blockBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var metadata = properties.Value.Metadata.ToDictionary(static k => k.Key, static v => v.Value);
        metadata[AzureStorageConstants.TusUploadLengthMetadataKey] = uploadLength.ToString();
        await blockBlobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
    }

    public async Task<long> GetCommittedStagingLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        var blockBlobClient = await GetBlockStagingBlobClientAsync(fileId, cancellationToken);
        if (blockBlobClient is null)
        {
            return 0;
        }

        try
        {
            var properties = await blockBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.ContentLength;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return 0;
        }
    }

    public async Task<long> ConcatenatePartialStagingBlobsAsync(
        string finalFileId,
        IReadOnlyList<string> partialFileIds,
        CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(finalFileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {finalFileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var finalBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, finalFileId));

        var (blockIds, totalLength) = await StagePartialBlobsForConcatenationAsync(
            finalBlobClient,
            containerClient,
            partialFileIds,
            cancellationToken);
        if (blockIds.Count == 0)
        {
            throw new InvalidOperationException($"Cannot concatenate partial uploads into file id {finalFileId} because no data was found.");
        }

        await finalBlobClient.CommitBlockListAsync(
            blockIds,
            new CommitBlockListOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/octet-stream"
                }
            },
            cancellationToken: cancellationToken);

        foreach (var partialFileId in partialFileIds)
        {
            var partialBlobClient = containerClient.GetBlockBlobClient(
                Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, partialFileId));
            await partialBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }

        return totalLength;
    }

    private async Task<(List<string> BlockIds, long TotalLength)> StagePartialBlobsForConcatenationAsync(
        BlockBlobClient finalBlobClient,
        BlobContainerClient containerClient,
        IReadOnlyList<string> partialFileIds,
        CancellationToken cancellationToken)
    {
        var stagingTasks = new Task<(List<string> BlockIds, long Length)>[partialFileIds.Count];
        for (var partialIndex = 0; partialIndex < partialFileIds.Count; partialIndex++)
        {
            stagingTasks[partialIndex] = StagePartialBlobForConcatenationAsync(
                finalBlobClient,
                containerClient,
                partialFileIds[partialIndex],
                partialIndex,
                cancellationToken);
        }

        var stagedPartials = await Task.WhenAll(stagingTasks);
        return (
            stagedPartials.SelectMany(static partial => partial.BlockIds).ToList(),
            stagedPartials.Sum(static partial => partial.Length));
    }

    private async Task<(List<string> BlockIds, long Length)> StagePartialBlobForConcatenationAsync(
        BlockBlobClient finalBlobClient,
        BlobContainerClient containerClient,
        string partialFileId,
        int partialIndex,
        CancellationToken cancellationToken)
    {
        var partialBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, partialFileId));
        if (!await partialBlobClient.ExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Partial staging blob {partialFileId} does not exist.");
        }

        var properties = await partialBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var partialLength = properties.Value.ContentLength;
        if (partialLength == 0)
        {
            throw new InvalidOperationException($"Partial staging blob {partialFileId} is empty.");
        }

        var chunkCount = (int)((partialLength + MaxPutBlockFromUrlSize - 1) / MaxPutBlockFromUrlSize);
        var (sourceUri, sourceAuthentication) = await ResolveSourceBlobAccessAsync(partialBlobClient, cancellationToken);
        var blockIds = new List<string>(chunkCount);
        var offset = 0L;

        for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            var chunkLength = Math.Min(MaxPutBlockFromUrlSize, partialLength - offset);
            var blockId = BuildConcatBlockId(partialIndex, chunkIndex);
            await StagePartialChunkForConcatenationAsync(
                finalBlobClient,
                partialBlobClient,
                sourceUri,
                sourceAuthentication,
                blockId,
                offset,
                chunkLength,
                cancellationToken);
            blockIds.Add(blockId);
            offset += chunkLength;
        }

        return (blockIds, partialLength);
    }

    private async Task StagePartialChunkForConcatenationAsync(
        BlockBlobClient finalBlobClient,
        BlockBlobClient partialBlobClient,
        Uri sourceUri,
        HttpAuthorization? sourceAuthentication,
        string blockId,
        long sourceOffset,
        long chunkLength,
        CancellationToken cancellationToken)
    {
        try
        {
            await finalBlobClient.StageBlockFromUriAsync(
                sourceUri,
                blockId,
                new StageBlockFromUriOptions
                {
                    SourceRange = new HttpRange(sourceOffset, chunkLength),
                    SourceAuthentication = sourceAuthentication
                },
                cancellationToken: cancellationToken);
        }
        catch (RequestFailedException) when (hostEnvironment.IsDevelopment())
        {
            await FallbackStagePartialBlobChunkAsync(
                finalBlobClient,
                partialBlobClient,
                blockId,
                sourceOffset,
                chunkLength,
                cancellationToken);
        }
    }

    private static async Task FallbackStagePartialBlobChunkAsync(
        BlockBlobClient finalBlobClient,
        BlockBlobClient partialBlobClient,
        string blockId,
        long sourceOffset,
        long chunkLength,
        CancellationToken cancellationToken)
    {
        await using var partialStream = await partialBlobClient.OpenReadAsync(
            position: sourceOffset,
            cancellationToken: cancellationToken);
        await using var chunkStream = new MemoryStream((int)chunkLength);
        var bytesCopied = 0L;
        var buffer = new byte[81920];
        while (bytesCopied < chunkLength)
        {
            var toRead = (int)Math.Min(buffer.Length, chunkLength - bytesCopied);
            var read = await partialStream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
            if (read == 0)
            {
                throw new InvalidOperationException(
                    $"Partial staging blob ended before reading {chunkLength} bytes at offset {sourceOffset}.");
            }

            await chunkStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            bytesCopied += read;
        }

        chunkStream.Position = 0;
        await finalBlobClient.StageBlockAsync(blockId, chunkStream, cancellationToken: cancellationToken);
    }

    private async Task<(Uri SourceUri, HttpAuthorization? SourceAuthentication)> ResolveSourceBlobAccessAsync(
        BlockBlobClient blobClient,
        CancellationToken cancellationToken)
    {
        if (blobClient.CanGenerateSasUri)
        {
            return (
                blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1)),
                null);
        }

        if (hostEnvironment.IsDevelopment())
        {
            return (blobClient.Uri, null);
        }

        var serviceClient = blobClient.GetParentBlobContainerClient().GetParentBlobServiceClient();
        var startsOn = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expiresOn = DateTimeOffset.UtcNow.AddHours(1);
        var userDelegationKey = await serviceClient.GetUserDelegationKeyAsync(startsOn, expiresOn, cancellationToken);
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b",
            StartsOn = startsOn,
            ExpiresOn = expiresOn
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return (blobClient.GenerateUserDelegationSasUri(sasBuilder, userDelegationKey.Value), null);
    }

    public async Task DeleteStagingBlobAsync(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return;
        }

        _blockBlobClients.TryRemove(BuildBlockBlobClientKey(storageContext, fileId), out _);
        var blockBlobClient = GetBlockBlobClient(storageContext, fileId);
        await blockBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private BlobContainerClient GetBlobContainerClient(TusStorageContext storageContext)
    {
        return _containerClients.GetOrAdd(storageContext.StorageAccountName, _ =>
        {
            if (hostEnvironment.IsDevelopment())
            {
                return new BlobServiceClient(AzureConstants.AzuriteUrl)
                    .GetBlobContainerClient(AzureStorageConstants.BrokerFilesContainerName);
            }

            var storageUri = new Uri(storageContext.ConnectionString);
            return new BlobServiceClient(storageUri, _azureCredential)
                .GetBlobContainerClient(AzureStorageConstants.BrokerFilesContainerName);
        });
    }

    private BlockBlobClient GetBlockBlobClient(TusStorageContext storageContext, string fileId)
        => _blockBlobClients.GetOrAdd(
            BuildBlockBlobClientKey(storageContext, fileId),
            _ => GetBlobContainerClient(storageContext).GetBlockBlobClient(
                Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId)));

    private static string BuildBlockBlobClientKey(TusStorageContext storageContext, string fileId)
        => $"{storageContext.StorageAccountName}:{fileId}";

    private async Task<TusStorageContext?> ResolveStorageContextAsync(
        string fileId,
        CancellationToken cancellationToken,
        TusUploadDebugTiming? timing = null)
    {
        var fileTransferId = await ResolveFileTransferIdAsync(fileId, cancellationToken, timing);
        if (fileTransferId is null)
        {
            timing?.Step("resolve.fileTransferIdNotFound");
            return null;
        }

        timing?.Step("resolve.fileTransferId", fileTransferId);
        return await ResolveStorageContextForFileTransferAsync(fileTransferId.Value, cancellationToken, timing);
    }

    private async Task<Guid?> ResolveFileTransferIdAsync(
        string fileId,
        CancellationToken cancellationToken,
        TusUploadDebugTiming? timing = null)
    {
        fileId = TusRouteHelper.NormalizePartialFileId(fileId);

        // Blocks stage on background tasks that outlive their PATCH. Reading a torn-down HttpContext
        // throws ObjectDisposedException or, once Kestrel has recycled it, NullReferenceException.
        // The route is only a hint: the lookups below resolve the same transfer without it.
        try
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is not null
                && TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var routeFileTransferId))
            {
                timing?.Step("resolve.fileTransferId.fromRoute", routeFileTransferId);
                return routeFileTransferId;
            }

            if (TusRouteHelper.TryGetFileTransferIdFromPath(httpContext?.Request.Path.Value, out var pathFileTransferId))
            {
                timing?.Step("resolve.fileTransferId.fromPath", pathFileTransferId);
                return pathFileTransferId;
            }
        }
        catch (Exception exception) when (exception is ObjectDisposedException or NullReferenceException)
        {
            timing?.Step("resolve.fileTransferId.requestEnded");
        }

        if (await partialUploadRegistry.TryGetFileTransferIdAsync(fileId, cancellationToken) is Guid mappedFileTransferId)
        {
            timing?.Step("resolve.fileTransferId.fromPartialRegistry", mappedFileTransferId);
            return mappedFileTransferId;
        }

        if (Guid.TryParse(fileId, out var parsedFileTransferId))
        {
            timing?.Step("resolve.fileTransferId.fromParse", parsedFileTransferId);
            return parsedFileTransferId;
        }

        return null;
    }

    private async Task<TusStorageContext?> ResolveStorageContextForFileTransferAsync(
        Guid fileTransferId,
        CancellationToken cancellationToken,
        TusUploadDebugTiming? timing = null)
    {
        if (_storageContextsByFileTransferId.TryGetValue(fileTransferId, out var cachedContext))
        {
            timing?.Step("resolve.contextCacheHit", cachedContext.StorageAccountName);
            return cachedContext;
        }

        timing?.Step("resolve.contextCacheMiss");

        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        timing?.Step("resolve.db.fileTransfer");
        if (fileTransfer is null)
        {
            return null;
        }

        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        timing?.Step("resolve.db.resource");
        if (resource is null)
        {
            return null;
        }

        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        timing?.Step("resolve.db.serviceOwner");
        if (serviceOwner is null)
        {
            return null;
        }

        var storageProvider = serviceOwner.GetStorageProvider(fileTransfer.UseVirusScan);
        if (storageProvider is null)
        {
            timing?.Step("resolve.storageProviderMissing");
            return null;
        }

        var connectionString = hostEnvironment.IsDevelopment()
            ? AzureConstants.AzuriteUrl
            : $"https://{storageProvider.ResourceName}.blob.core.windows.net";

        var context = new TusStorageContext(connectionString, storageProvider.ResourceName, fileTransfer.StripeSizeBytes ?? 0);
        _storageContextsByFileTransferId.TryAdd(fileTransferId, context);
        timing?.Step("resolve.contextBuilt", storageProvider.ResourceName);
        return context;
    }

    /// <summary>
    /// Encodes both partial and chunk indices so block ids stay unique across the final commit list.
    /// </summary>
    private static string BuildConcatBlockId(int partialIndex, int chunkIndex)
    {
        var blockId = $"{partialIndex:D6}{chunkIndex:D6}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(blockId));
    }

    private sealed record TusStorageContext(
        string ConnectionString,
        string StorageAccountName,
        long StripeSizeBytes);
}
