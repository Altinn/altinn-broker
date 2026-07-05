namespace Altinn.Broker.Application.UploadFile;

public interface ITusUploadFinalizationService
{
    Task EnsureFinalConcatenatedAsync(string tusFileId, CancellationToken cancellationToken);

    Task FinalizeStagingAsync(string tusFileId, CancellationToken cancellationToken);

    Task<bool> IsReadyForTransferCompletionAsync(string tusFileId, CancellationToken cancellationToken);

    Task<bool> IsReadyForStagingFinalizeAsync(string tusFileId, CancellationToken cancellationToken);

    Task<bool> IsPartialUploadAsync(string tusFileId, CancellationToken cancellationToken);

    Task CleanupCompletedUploadAsync(string tusFileId, CancellationToken cancellationToken);
}
