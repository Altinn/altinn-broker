namespace Altinn.Broker.Application.UploadFile;

public interface ITusFinalizeUploadEnqueuer
{
    Task<bool> EnqueueConcatenateAsync(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken);

    bool EnqueuePublish(Guid fileTransferId, string tusFileId);
}
