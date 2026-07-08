using Altinn.Broker.Application.UploadFile;

namespace Altinn.Broker.Integrations.Tus;

public sealed class TusUploadKindResolver(ITusPartialUploadRegistry partialUploadRegistry) : ITusUploadKindResolver
{
    public Task<bool> IsPartialUploadAsync(string tusFileId, CancellationToken cancellationToken)
        => partialUploadRegistry.IsPartialAsync(TusRouteHelper.NormalizePartialFileId(tusFileId), cancellationToken);
}
