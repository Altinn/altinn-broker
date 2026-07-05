using Hangfire;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.UploadFile;

public class TusFinalizeUploadHandler(
    ITusUploadFinalizationService tusUploadFinalizationService,
    TusUploadCompleteHandler tusUploadCompleteHandler,
    ILogger<TusFinalizeUploadHandler> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task Process(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "TUS finalize job starting for file transfer {FileTransferId}. TusFileId={TusFileId}",
            fileTransferId,
            tusFileId);

        if (await tusUploadFinalizationService.IsPartialUploadAsync(tusFileId, cancellationToken))
        {
            logger.LogInformation(
                "TUS finalize job committing staging for partial tus file {TusFileId}",
                tusFileId);
            await tusUploadFinalizationService.FinalizeStagingAsync(tusFileId, cancellationToken);
            return;
        }

        await tusUploadFinalizationService.FinalizeStagingAsync(tusFileId, cancellationToken);

        var result = await tusUploadCompleteHandler.Process(fileTransferId, user: null, cancellationToken);
        if (result.IsT1)
        {
            throw new InvalidOperationException(
                $"TUS finalize job failed for file transfer {fileTransferId}: {result.AsT1.Message}");
        }

        await tusUploadFinalizationService.CleanupCompletedUploadAsync(fileTransferId.ToString(), cancellationToken);

        logger.LogInformation(
            "TUS finalize job completed for file transfer {FileTransferId}",
            fileTransferId);
    }
}
