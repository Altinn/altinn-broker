namespace Altinn.Broker.Application.UploadFile;

public interface ITusFinalizeUploadEnqueuer
{
    Task EnqueueAsync(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken);
}
