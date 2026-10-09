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
        string blockId,
        Stream blockData,
        CancellationToken cancellationToken)
    {
        if (failureController.TryConsumeFailure())
        {
            throw new InvalidOperationException(
                $"Injected TUS destination staging failure for file id {fileId}, block {blockId}.");
        }

        return inner.StageTusBlockOnDestinationAsync(fileId, blockId, blockData, cancellationToken);
    }

    public Task CommitTusBlocksAsync(
        string fileId,
        IReadOnlyList<string> blockIds,
        CancellationToken cancellationToken)
        => inner.CommitTusBlocksAsync(fileId, blockIds, cancellationToken);

    public Task CommitBlocksToDestinationAsync(
        string fileId,
        IReadOnlyList<string> blockIds,
        CancellationToken cancellationToken)
        => inner.CommitBlocksToDestinationAsync(fileId, blockIds, cancellationToken);

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

    public Task<TusStagedBlocksSnapshot?> TryGetDestinationStagedBlocksSnapshotAsync(
        string fileId,
        CancellationToken cancellationToken)
        => inner.TryGetDestinationStagedBlocksSnapshotAsync(fileId, cancellationToken);

    public Task<long> GetDestinationUncommittedBlocksLengthAsync(
        string fileId,
        CancellationToken cancellationToken)
        => inner.GetDestinationUncommittedBlocksLengthAsync(fileId, cancellationToken);

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

    public Task<bool> DestinationBlobExistsAsync(string fileId, CancellationToken cancellationToken)
        => inner.DestinationBlobExistsAsync(fileId, cancellationToken);

    public Task<long> GetDestinationBlobLengthAsync(string fileId, CancellationToken cancellationToken)
        => inner.GetDestinationBlobLengthAsync(fileId, cancellationToken);
}
