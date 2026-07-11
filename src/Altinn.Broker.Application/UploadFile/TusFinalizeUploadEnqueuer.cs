using Hangfire;

namespace Altinn.Broker.Application.UploadFile;

public sealed class TusFinalizeUploadEnqueuer(
    IBackgroundJobClient backgroundJobClient,
    ITusConcatJobCoordinator concatJobCoordinator) : ITusFinalizeUploadEnqueuer
{
    public async Task<bool> EnqueueConcatenateAsync(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken)
    {
        if (!await concatJobCoordinator.TryAcquireEnqueueSlotAsync(tusFileId, cancellationToken))
        {
            return false;
        }

        backgroundJobClient.Enqueue<TusConcatenateUploadHandler>(handler =>
            handler.Process(fileTransferId, tusFileId, CancellationToken.None));
        return true;
    }

    public bool EnqueuePublish(Guid fileTransferId, string tusFileId)
    {
        if (!concatJobCoordinator.TryAcquirePublishEnqueueSlotAsync(tusFileId, CancellationToken.None)
                .GetAwaiter()
                .GetResult())
        {
            return false;
        }

        backgroundJobClient.Enqueue<TusPublishUploadHandler>(handler =>
            handler.Process(fileTransferId, tusFileId, CancellationToken.None));
        return true;
    }
}
