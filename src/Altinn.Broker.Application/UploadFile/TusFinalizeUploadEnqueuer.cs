using Hangfire;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.UploadFile;

public sealed class TusFinalizeUploadEnqueuer(
    IBackgroundJobClient backgroundJobClient,
    ITusConcatJobCoordinator concatJobCoordinator,
    ILogger<TusFinalizeUploadEnqueuer> logger) : ITusFinalizeUploadEnqueuer
{
    public async Task<bool> EnqueueConcatenateAsync(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken)
    {
        if (!await concatJobCoordinator.TryAcquireEnqueueSlotAsync(tusFileId, cancellationToken))
        {
            logger.LogWarning(
                "Skipped TUS concatenate enqueue for file transfer {FileTransferId}. TusFileId={TusFileId}. Enqueue slot was not acquired.",
                fileTransferId,
                tusFileId);
            return false;
        }

        try
        {
            backgroundJobClient.Enqueue<TusConcatenateUploadHandler>(handler =>
                handler.Process(fileTransferId, tusFileId, CancellationToken.None));

            logger.LogInformation(
                "Enqueued TUS concatenate job for file transfer {FileTransferId}. TusFileId={TusFileId}",
                fileTransferId,
                tusFileId);
            return true;
        }
        catch (Exception ex)
        {
            await concatJobCoordinator.ClearConcatEnqueueSlotAsync(tusFileId, cancellationToken);
            logger.LogError(
                ex,
                "Failed to enqueue TUS concatenate job for file transfer {FileTransferId}. TusFileId={TusFileId}",
                fileTransferId,
                tusFileId);
            throw;
        }
    }

    public bool EnqueuePublish(Guid fileTransferId, string tusFileId)
    {
        if (!concatJobCoordinator.TryAcquirePublishEnqueueSlotAsync(tusFileId, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
        {
            logger.LogWarning(
                "Skipped TUS publish enqueue for file transfer {FileTransferId}. TusFileId={TusFileId}. Enqueue slot was not acquired.",
                fileTransferId,
                tusFileId);
            return false;
        }

        backgroundJobClient.Enqueue<TusPublishUploadHandler>(handler =>
            handler.Process(fileTransferId, tusFileId, CancellationToken.None));

        logger.LogInformation(
            "Enqueued TUS publish job for file transfer {FileTransferId}. TusFileId={TusFileId}",
            fileTransferId,
            tusFileId);
        return true;
    }
}
