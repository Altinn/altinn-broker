namespace Altinn.Broker.Application.UploadFile;

public interface ITusConcatCheckpointStore
{
    Task<TusConcatCheckpoint?> TryGetCheckpointAsync(string tusFileId, CancellationToken cancellationToken);

    Task SaveCheckpointAsync(string tusFileId, TusConcatCheckpoint checkpoint, CancellationToken cancellationToken);

    Task ClearCheckpointAsync(string tusFileId, CancellationToken cancellationToken);
}
