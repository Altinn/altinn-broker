using Hangfire;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.UploadFile;

public class TusConcatenateUploadHandler(
    ITusUploadFinalizationService tusUploadFinalizationService,
    ITusFinalizeUploadEnqueuer tusFinalizeUploadEnqueuer,
    ITusConcatJobCoordinator concatJobCoordinator,
    ILogger<TusConcatenateUploadHandler> logger)
{
    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution("tus-concat:{1}", 28800)]
    public async Task Process(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken)
    {
        if (!await concatJobCoordinator.TryBeginJobAsync(tusFileId, cancellationToken))
        {
            if (await concatJobCoordinator.IsConcatCompleteAsync(tusFileId, cancellationToken)
                && await tusUploadFinalizationService.IsStagingBlobCommittedAsync(tusFileId, cancellationToken))
            {
                logger.LogInformation(
                    "TUS concatenate job skipped because concat is already complete for file transfer {FileTransferId}. Enqueueing publish.",
                    fileTransferId);
                await concatJobCoordinator.ClearPublishEnqueueSlotAsync(tusFileId, cancellationToken);
                tusFinalizeUploadEnqueuer.EnqueuePublish(fileTransferId, tusFileId);
            }
            else if (await concatJobCoordinator.IsConcatCompleteAsync(tusFileId, cancellationToken))
            {
                logger.LogWarning(
                    "TUS concatenate job skipped for file transfer {FileTransferId}: concat is marked complete but destination blob is not committed.",
                    fileTransferId);
            }
            else
            {
                logger.LogInformation(
                    "TUS concatenate job skipped for file transfer {FileTransferId}. Another worker is already processing TusFileId={TusFileId}.",
                    fileTransferId,
                    tusFileId);
            }

            return;
        }

        try
        {
            logger.LogInformation(
                "TUS concatenate job starting for file transfer {FileTransferId}. TusFileId={TusFileId}",
                fileTransferId,
                tusFileId);

            if (await tusUploadFinalizationService.IsPartialUploadAsync(tusFileId, cancellationToken))
            {
                logger.LogWarning(
                    "TUS concatenate job received partial tus file {TusFileId}; partial finalize jobs are no longer used in Phase B.",
                    tusFileId);
                return;
            }

            await tusUploadFinalizationService.EnsureFinalConcatenatedAsync(tusFileId, cancellationToken);

            if (!await concatJobCoordinator.IsConcatCompleteAsync(tusFileId, cancellationToken))
            {
                logger.LogWarning(
                    "TUS concatenate job finished without completing concat for file transfer {FileTransferId}. TusFileId={TusFileId}",
                    fileTransferId,
                    tusFileId);
                return;
            }

            if (!await tusUploadFinalizationService.IsStagingBlobCommittedAsync(tusFileId, cancellationToken))
            {
                logger.LogWarning(
                    "TUS concatenate job finished for file transfer {FileTransferId}, but destination blob is not committed. Not enqueueing publish.",
                    fileTransferId);
                return;
            }

            logger.LogInformation(
                "TUS concatenate job completed for file transfer {FileTransferId}. Enqueueing publish job.",
                fileTransferId);

            tusFinalizeUploadEnqueuer.EnqueuePublish(fileTransferId, tusFileId);
        }
        finally
        {
            await concatJobCoordinator.ReleaseRunningLockAsync(tusFileId, cancellationToken);
        }
    }
}
