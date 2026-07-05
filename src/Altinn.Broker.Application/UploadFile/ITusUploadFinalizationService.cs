namespace Altinn.Broker.Application.UploadFile;

public interface ITusUploadFinalizationService
{
    Task FinalizeStagingAsync(string tusFileId, CancellationToken cancellationToken);

    Task<bool> IsReadyForTransferCompletionAsync(string tusFileId, CancellationToken cancellationToken);

    Task<bool> IsPartialUploadAsync(string tusFileId, CancellationToken cancellationToken);

    Task CleanupCompletedUploadAsync(string tusFileId, CancellationToken cancellationToken);
}
