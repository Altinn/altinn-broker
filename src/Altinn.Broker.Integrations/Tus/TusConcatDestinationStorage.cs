using Altinn.Broker.Core.Domain;
using Altinn.Broker.Integrations.Azure;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace Altinn.Broker.Integrations.Tus;

internal static class TusConcatDestinationStorage
{
    public static BlockBlobClient GetDestinationBlockBlobClient(BlobContainerClient containerClient, Guid fileTransferId, int stripeIndex = 0)
        => containerClient.GetBlockBlobClient(StripeLayout.GetStripeBlobName(fileTransferId, stripeIndex));

    public static async Task<IReadOnlyList<BlobBlock>?> TryGetUncommittedBlocksAsync(
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

    public static async Task<TusStagedBlocksSnapshot?> TryGetStagedBlocksSnapshotAsync(
        BlockBlobClient blockBlobClient,
        CancellationToken cancellationToken)
    {
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

    public static async Task SetUploadLengthMetadataAsync(
        BlockBlobClient blockBlobClient,
        long uploadLength,
        CancellationToken cancellationToken)
    {
        try
        {
            var properties = await blockBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var metadata = properties.Value.Metadata.ToDictionary(static k => k.Key, static v => v.Value);
            metadata[AzureStorageConstants.TusUploadLengthMetadataKey] = uploadLength.ToString();
            await blockBlobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
        }
    }
}
