using Altinn.Broker.Application.UploadFile.Tus;

namespace Altinn.Broker.Integrations.Tus;

public sealed class TusPartialUploadLookup(ITusPartialUploadRegistry partialUploadRegistry) : ITusPartialUploadLookup
{
    public Task<IReadOnlyList<TusPartialUpload>> GetPartialUploadsAsync(Guid fileTransferId, CancellationToken cancellationToken)
        => partialUploadRegistry.GetPartialUploadsAsync(fileTransferId, cancellationToken);
}
