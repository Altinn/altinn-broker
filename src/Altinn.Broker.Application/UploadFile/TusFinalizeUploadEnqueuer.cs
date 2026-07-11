using Hangfire;

namespace Altinn.Broker.Application.UploadFile;

public sealed class TusFinalizeUploadEnqueuer(IBackgroundJobClient backgroundJobClient) : ITusFinalizeUploadEnqueuer
{
    public Task EnqueueConcatenateAsync(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken)
    {
        backgroundJobClient.Enqueue<TusConcatenateUploadHandler>(handler =>
            handler.Process(fileTransferId, tusFileId, CancellationToken.None));
        return Task.CompletedTask;
    }

    public void EnqueuePublish(Guid fileTransferId, string tusFileId)
        => backgroundJobClient.Enqueue<TusPublishUploadHandler>(handler =>
            handler.Process(fileTransferId, tusFileId, CancellationToken.None));
}
