using Hangfire;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.UploadFile;

public class TusConcatenateUploadHandler(
    ITusUploadFinalizationService tusUploadFinalizationService,
    ILogger<TusConcatenateUploadHandler> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task Process(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "TUS concatenate job starting for file transfer {FileTransferId}. TusFileId={TusFileId}",
            fileTransferId,
            tusFileId);

        if (await tusUploadFinalizationService.IsPartialUploadAsync(tusFileId, cancellationToken))
        {
            logger.LogInformation(
                "TUS concatenate job committing staging for partial tus file {TusFileId}",
                tusFileId);
            await tusUploadFinalizationService.FinalizeStagingAsync(tusFileId, cancellationToken);
            return;
        }

        await tusUploadFinalizationService.EnsureFinalConcatenatedAsync(tusFileId, cancellationToken);

        logger.LogInformation(
            "TUS concatenate job completed for file transfer {FileTransferId}",
            fileTransferId);
    }
}
