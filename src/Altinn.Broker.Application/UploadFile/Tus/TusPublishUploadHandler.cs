using Hangfire;
using Hangfire.Server;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.UploadFile.Tus;

public class TusPublishUploadHandler(
    ITusUploadFinalizationService tusUploadFinalizationService,
    TusUploadCompleteHandler tusUploadCompleteHandler,
    ITusConcatJobCoordinator concatJobCoordinator,
    ILogger<TusPublishUploadHandler> logger)
{
    private const int RetryAttempts = 3;

    [AutomaticRetry(Attempts = RetryAttempts)]
    [DisableConcurrentExecution("tus-publish:{1}", 7200)]
    public async Task Process(
        Guid fileTransferId,
        string tusFileId,
        CancellationToken cancellationToken,
        PerformContext? performContext)
    {
        try
        {
            logger.LogInformation(
                "TUS publish job starting for file transfer {FileTransferId}. TusFileId={TusFileId}",
                fileTransferId,
                tusFileId);

            if (await tusUploadFinalizationService.IsConcatMarkedCompleteAsync(tusFileId, cancellationToken))
            {
                if (!await tusUploadFinalizationService.IsStagingBlobCommittedAsync(tusFileId, cancellationToken))
                {
                    throw new InvalidOperationException(
                        $"TUS publish aborted for file transfer {fileTransferId}: concat is complete but destination blob is missing or incomplete.");
                }

                logger.LogInformation(
                    "TUS publish skipping staging finalize for file transfer {FileTransferId}; concat already committed destination blob.",
                    fileTransferId);
            }
            else
            {
                await tusUploadFinalizationService.FinalizeStagingAsync(tusFileId, cancellationToken);
            }

            var result = await tusUploadCompleteHandler.Process(fileTransferId, user: null, cancellationToken);
            if (result.IsT1)
            {
                throw new InvalidOperationException(
                    $"TUS publish job failed for file transfer {fileTransferId}: {result.AsT1.Message}");
            }

            await tusUploadFinalizationService.CleanupCompletedUploadAsync(fileTransferId.ToString(), cancellationToken);

            logger.LogInformation(
                "TUS publish job completed for file transfer {FileTransferId}",
                fileTransferId);

            await concatJobCoordinator.ClearPublishEnqueueSlotAsync(tusFileId, CancellationToken.None);
        }
        catch (Exception) when (ShouldReleasePublishSlotAfterFailure(performContext))
        {
            await concatJobCoordinator.ClearPublishEnqueueSlotAsync(tusFileId, CancellationToken.None);
            throw;
        }
    }

    private static bool ShouldReleasePublishSlotAfterFailure(PerformContext? performContext)
    {
        if (performContext is null)
        {
            return true;
        }

        var retryCount = performContext.GetJobParameter<int>("RetryCount");
        return retryCount >= RetryAttempts;
    }
}
