using Altinn.Broker.Application.UploadFile;

namespace Altinn.Broker.Integrations.Tus;

public sealed class TusUploadFinalizationService(
    BrokerTusStore store,
    ITusPartialUploadRegistry partialUploadRegistry) : ITusUploadFinalizationService
{
    public Task FinalizeStagingAsync(string tusFileId, CancellationToken cancellationToken)
        => store.FinalizeStagingFromDurableStateAsync(tusFileId, cancellationToken);

    public Task<bool> IsReadyForTransferCompletionAsync(string tusFileId, CancellationToken cancellationToken)
        => store.IsReadyForTransferCompletionAsync(tusFileId, cancellationToken);

    public Task<bool> IsReadyForStagingFinalizeAsync(string tusFileId, CancellationToken cancellationToken)
        => store.IsReadyForStagingFinalizeAsync(tusFileId, cancellationToken);

    public async Task<bool> IsPartialUploadAsync(string tusFileId, CancellationToken cancellationToken)
        => await partialUploadRegistry.IsPartialAsync(TusRouteHelper.NormalizePartialFileId(tusFileId), cancellationToken);

    public Task CleanupCompletedUploadAsync(string tusFileId, CancellationToken cancellationToken)
        => store.CleanupCompletedUploadAsync(tusFileId, cancellationToken);
}
