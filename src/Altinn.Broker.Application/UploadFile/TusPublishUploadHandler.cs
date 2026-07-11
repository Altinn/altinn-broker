using Hangfire;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.UploadFile;

public class TusPublishUploadHandler(
    ITusUploadFinalizationService tusUploadFinalizationService,
    TusUploadCompleteHandler tusUploadCompleteHandler,
    ITusConcatJobCoordinator concatJobCoordinator,
    ILogger<TusPublishUploadHandler> logger)
{
    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 7200)]
    public async Task Process(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "TUS publish job starting for file transfer {FileTransferId}. TusFileId={TusFileId}",
                fileTransferId,
                tusFileId);

            await tusUploadFinalizationService.FinalizeStagingAsync(tusFileId, cancellationToken);

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
        }
        finally
        {
            await concatJobCoordinator.ClearPublishEnqueueSlotAsync(tusFileId, cancellationToken);
        }
    }
}
