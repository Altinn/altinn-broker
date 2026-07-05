using Hangfire;

namespace Altinn.Broker.Application.UploadFile;

public sealed class TusFinalizeUploadEnqueuer(IBackgroundJobClient backgroundJobClient) : ITusFinalizeUploadEnqueuer
{
    public void Enqueue(Guid fileTransferId, string tusFileId)
        => backgroundJobClient.Enqueue<TusFinalizeUploadHandler>(handler =>
            handler.Process(fileTransferId, tusFileId, CancellationToken.None));
}
