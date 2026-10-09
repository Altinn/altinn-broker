using Altinn.Broker.Integrations.Azure;
using Altinn.Broker.Integrations.Tus;

namespace Altinn.Broker.Tests.Helpers;

/// <summary>
/// Forwards to the real <see cref="ITusStorageResolver"/> except when
/// <see cref="TusStagingFailureController"/> requests a staging failure.
/// </summary>
internal sealed class FailingTusStorageResolverDecorator(
    ITusStorageResolver inner,
    TusStagingFailureController failureController) : ITusStorageResolver
{
    public Task<long> StageTusBlockAsync(
        string fileId,
        string blockId,
        Stream blockData,
        CancellationToken cancellationToken)
    {
        if (failureController.TryConsumeFailure())
        {
            throw new InvalidOperationException(
                $"Injected TUS staging failure for file id {fileId}, block {blockId}.");
        }

        return inner.StageTusBlockAsync(fileId, blockId, blockData, cancellationToken);
    }

    public Task<long> StageTusBlockOnDestinationAsync(
        string fileId,
        int stripeIndex,
        string blockId,
        Stream blockData,
        CancellationToken cancellationToken)
    {
        if (failureController.TryConsumeFailure())
        {
            throw new InvalidOperationException(
                $"Injected TUS destination staging failure for file id {fileId}, block {blockId}.");
        }

        return inner.StageTusBlockOnDestinationAsync(fileId, stripeIndex, blockId, blockData, cancellationToken);
    }

    public Task CommitTusBlocksAsync(
        string fileId,
        IReadOnlyList<string> blockIds,
        CancellationToken cancellationToken)
        => inner.CommitTusBlocksAsync(fileId, blockIds, cancellationToken);

    public Task CommitStripeBlocksAsync(
        string fileId,
        int stripeIndex,
        IReadOnlyList<string> blockIds,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
        => inner.CommitStripeBlocksAsync(fileId, stripeIndex, blockIds, metadata, cancellationToken);

    public Task<TusStagedBlocksSnapshot?> TryGetStripeStagedBlocksSnapshotAsync(
        string fileId,
        int stripeIndex,
        CancellationToken cancellationToken)
        => inner.TryGetStripeStagedBlocksSnapshotAsync(fileId, stripeIndex, cancellationToken);

    public Task<long> GetStripeBlobLengthAsync(string fileId, int stripeIndex, CancellationToken cancellationToken)
        => inner.GetStripeBlobLengthAsync(fileId, stripeIndex, cancellationToken);

    public Task<CommittedStripes> GetCommittedStripesAsync(string fileId, CancellationToken cancellationToken)
        => inner.GetCommittedStripesAsync(fileId, cancellationToken);

    public Task DeleteAllStripesAsync(string fileId, CancellationToken cancellationToken)
        => inner.DeleteAllStripesAsync(fileId, cancellationToken);

    public Task<long> GetStripeSizeAsync(string fileId, CancellationToken cancellationToken)
        => inner.GetStripeSizeAsync(fileId, cancellationToken);

    public Task<byte[]> ComputeCommittedStagingMd5Async(string fileId, CancellationToken cancellationToken)
        => inner.ComputeCommittedStagingMd5Async(fileId, cancellationToken);

    public Task SetCommittedStagingMd5Async(string fileId, byte[] md5Hash, CancellationToken cancellationToken)
        => inner.SetCommittedStagingMd5Async(fileId, md5Hash, cancellationToken);

    public Task<bool> StagingBlobExistsAsync(string fileId, CancellationToken cancellationToken)
        => inner.StagingBlobExistsAsync(fileId, cancellationToken);

    public Task<bool> HasStagedBlocksAsync(string fileId, CancellationToken cancellationToken)
        => inner.HasStagedBlocksAsync(fileId, cancellationToken);

    public Task<long> GetCommittedStagingLengthAsync(string fileId, CancellationToken cancellationToken)
        => inner.GetCommittedStagingLengthAsync(fileId, cancellationToken);

    public Task<long> GetStagedBlocksLengthAsync(string fileId, CancellationToken cancellationToken)
        => inner.GetStagedBlocksLengthAsync(fileId, cancellationToken);

    public Task<TusStagedBlocksSnapshot?> TryGetStagedBlocksSnapshotAsync(
        string fileId,
        CancellationToken cancellationToken)
        => inner.TryGetStagedBlocksSnapshotAsync(fileId, cancellationToken);

    public Task<long> GetDestinationUncommittedBlocksLengthAsync(
        string fileId,
        int stripeIndex,
        CancellationToken cancellationToken)
        => inner.GetDestinationUncommittedBlocksLengthAsync(fileId, stripeIndex, cancellationToken);

    public Task<long?> TryGetStagingUploadLengthAsync(string fileId, CancellationToken cancellationToken)
        => inner.TryGetStagingUploadLengthAsync(fileId, cancellationToken);

    public Task InitializePartialStagingBlobAsync(
        string fileId,
        long uploadLength,
        CancellationToken cancellationToken)
        => inner.InitializePartialStagingBlobAsync(fileId, uploadLength, cancellationToken);

    public Task SetStagingUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
        => inner.SetStagingUploadLengthAsync(fileId, uploadLength, cancellationToken);

    public Task SetDestinationUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
        => inner.SetDestinationUploadLengthAsync(fileId, uploadLength, cancellationToken);

    public Task<long> ConcatenatePartialStagingBlobsAsync(
        string finalFileId,
        IReadOnlyList<string> partialFileIds,
        CancellationToken cancellationToken)
        => inner.ConcatenatePartialStagingBlobsAsync(finalFileId, partialFileIds, cancellationToken);

    public Task DeleteStagingBlobAsync(string fileId, CancellationToken cancellationToken)
        => inner.DeleteStagingBlobAsync(fileId, cancellationToken);
}
