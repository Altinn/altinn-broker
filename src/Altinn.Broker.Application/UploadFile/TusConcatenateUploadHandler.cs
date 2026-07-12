using Hangfire;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.UploadFile;

public class TusConcatenateUploadHandler(
    ITusUploadFinalizationService tusUploadFinalizationService,
    ITusFinalizeUploadEnqueuer tusFinalizeUploadEnqueuer,
    ITusConcatJobCoordinator concatJobCoordinator,
    ILogger<TusConcatenateUploadHandler> logger)
{
    private static readonly TimeSpan PartialWaitRetryDelay = TimeSpan.FromSeconds(5);

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution("tus-concat:{1}", 1800)]
    public Task Process(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken)
        => ProcessChainStep(fileTransferId, tusFileId, cancellationToken);

    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution("tus-concat:{1}", 1800)]
    public async Task ProcessChainStep(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken)
    {
        if (await tusUploadFinalizationService.IsPartialUploadAsync(tusFileId, cancellationToken))
        {
            logger.LogWarning(
                "TUS concatenate job received partial tus file {TusFileId}; partial finalize jobs are no longer used.",
                tusFileId);
            return;
        }

        if (!await concatJobCoordinator.TryBeginJobAsync(tusFileId, cancellationToken))
        {
            if (await concatJobCoordinator.IsConcatCompleteAsync(tusFileId, cancellationToken)
                && await tusUploadFinalizationService.IsStagingBlobCommittedAsync(tusFileId, cancellationToken))
            {
                logger.LogInformation(
                    "TUS concat chain skipped because concat is already complete for file transfer {FileTransferId}. Enqueueing publish.",
                    fileTransferId);
                await concatJobCoordinator.ClearConcatEnqueueSlotAsync(tusFileId, cancellationToken);
                await concatJobCoordinator.ClearPublishEnqueueSlotAsync(tusFileId, cancellationToken);
                tusFinalizeUploadEnqueuer.EnqueuePublish(fileTransferId, tusFileId);
            }
            else if (await concatJobCoordinator.IsConcatCompleteAsync(tusFileId, cancellationToken))
            {
                logger.LogWarning(
                    "TUS concat chain skipped for file transfer {FileTransferId}: concat is marked complete but destination blob is not committed.",
                    fileTransferId);
            }
            else
            {
                logger.LogInformation(
                    "TUS concat chain step skipped for file transfer {FileTransferId}. Another worker is processing TusFileId={TusFileId}.",
                    fileTransferId,
                    tusFileId);
            }

            return;
        }

        try
        {
            logger.LogInformation(
                "TUS concat chain step starting for file transfer {FileTransferId}. TusFileId={TusFileId}",
                fileTransferId,
                tusFileId);

            var result = await tusUploadFinalizationService.ProcessConcatChainStepAsync(tusFileId, cancellationToken);

            if (result.ChainComplete)
            {
                if (!await tusUploadFinalizationService.IsStagingBlobCommittedAsync(tusFileId, cancellationToken))
                {
                    logger.LogWarning(
                        "TUS concat chain completed for file transfer {FileTransferId}, but destination blob is not committed. Not enqueueing publish.",
                        fileTransferId);
                    return;
                }

                logger.LogInformation(
                    "TUS concat chain completed for file transfer {FileTransferId}. Enqueueing publish job.",
                    fileTransferId);

                await concatJobCoordinator.ClearConcatEnqueueSlotAsync(tusFileId, cancellationToken);
                await concatJobCoordinator.ClearPublishEnqueueSlotAsync(tusFileId, cancellationToken);
                tusFinalizeUploadEnqueuer.EnqueuePublish(fileTransferId, tusFileId);
                return;
            }

            if (!result.StepCompleted && result.ShouldRetryStep)
            {
                logger.LogInformation(
                    "TUS concat chain step deferred for file transfer {FileTransferId}. Retrying in {RetryDelaySeconds}s.",
                    fileTransferId,
                    PartialWaitRetryDelay.TotalSeconds);

                await tusFinalizeUploadEnqueuer.ScheduleConcatChainStepAsync(
                    fileTransferId,
                    tusFileId,
                    PartialWaitRetryDelay,
                    cancellationToken);
                return;
            }

            if (result.StepCompleted)
            {
                logger.LogInformation(
                    "TUS concat chain advanced for file transfer {FileTransferId}. NextStep={NextStep}",
                    fileTransferId,
                    result.NextStep);

                await tusFinalizeUploadEnqueuer.EnqueueConcatChainStepAsync(
                    fileTransferId,
                    tusFileId,
                    cancellationToken);
                return;
            }

            logger.LogWarning(
                "TUS concat chain step finished without advancing for file transfer {FileTransferId}. TusFileId={TusFileId}",
                fileTransferId,
                tusFileId);
        }
        finally
        {
            await concatJobCoordinator.ReleaseRunningLockAsync(tusFileId, cancellationToken);
        }
    }
}
