namespace Altinn.Broker.Application.UploadFile;

public interface ITusFinalizeUploadEnqueuer
{
    void Enqueue(Guid fileTransferId, string tusFileId);
}
