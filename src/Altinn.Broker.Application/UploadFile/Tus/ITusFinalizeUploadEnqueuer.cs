namespace Altinn.Broker.Application.UploadFile.Tus;

public interface ITusFinalizeUploadEnqueuer
{
    Task<bool> EnqueueConcatenateAsync(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken);

    Task EnqueueConcatChainStepAsync(Guid fileTransferId, string tusFileId, CancellationToken cancellationToken);

    Task ScheduleConcatChainStepAsync(
        Guid fileTransferId,
        string tusFileId,
        TimeSpan delay,
        CancellationToken cancellationToken);

    bool EnqueuePublish(Guid fileTransferId, string tusFileId);
}
