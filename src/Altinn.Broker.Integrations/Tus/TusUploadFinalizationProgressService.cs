using Altinn.Broker.Application.UploadFile.Tus;

namespace Altinn.Broker.Integrations.Tus;

public sealed class TusUploadFinalizationProgressService(
    ITusPartialUploadRegistry partialUploadRegistry,
    ITusConcatCheckpointStore concatCheckpointStore,
    ITusStorageResolver storageResolver) : ITusUploadFinalizationProgressService
{
    public async Task<bool> IsTusFinalizationInProgressAsync(Guid fileTransferId, CancellationToken cancellationToken)
    {
        var tusFileId = fileTransferId.ToString();

        if (await concatCheckpointStore.TryGetCheckpointAsync(tusFileId, cancellationToken) is not null)
        {
            return true;
        }

        if (await partialUploadRegistry.TryGetConcatStatusAsync(tusFileId, cancellationToken)
            is TusConcatStatus.Pending or TusConcatStatus.InProgress)
        {
            return true;
        }

        if (await partialUploadRegistry.TryGetFinalConcatPartialIdsAsync(tusFileId, cancellationToken) is { Length: > 0 }
            && await partialUploadRegistry.TryGetConcatStatusAsync(tusFileId, cancellationToken) != TusConcatStatus.Complete)
        {
            return true;
        }

        // Without a known target length there is nothing to compare against, so committed stripes cannot
        // show the upload is finished.
        if (await partialUploadRegistry.TryGetUploadLengthAsync(tusFileId, cancellationToken) is { } uploadLength)
        {
            var committedStripes = await storageResolver.GetCommittedStripesAsync(tusFileId, cancellationToken);
            if (committedStripes.StripeCount > 0 && committedStripes.TotalLength >= uploadLength)
            {
                return false;
            }
        }

        if (await storageResolver.StagingBlobExistsAsync(tusFileId, cancellationToken)
            || await storageResolver.HasStagedBlocksAsync(tusFileId, cancellationToken))
        {
            return true;
        }

        return await partialUploadRegistry.IsKnownUploadAsync(tusFileId, cancellationToken);
    }
}
