namespace Altinn.Broker.Application.UploadFile;

public interface ITusFinalizeUploadEnqueuer
{
    Task EnqueueConcatenateAsync(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken);

    void EnqueuePublish(Guid fileTransferId, string tusFileId);
}
