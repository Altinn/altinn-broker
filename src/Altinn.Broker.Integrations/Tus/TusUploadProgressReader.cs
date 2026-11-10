using Altinn.Broker.Application.UploadPlans;

namespace Altinn.Broker.Integrations.Tus;

/// <summary>
/// Reads the progress cache directly, without going through the tus store. The store resolves storage
/// from the request route and heals offsets as it reads them, neither of which belongs in a plain lookup.
/// </summary>
public sealed class TusUploadProgressReader(ITusUploadProgressCache uploadProgressCache) : ITusUploadProgressReader
{
    public async Task<long> GetUploadedBytesAsync(Guid fileTransferId, string uploadId, CancellationToken cancellationToken)
    {
        // A file sent whole is stored under the transfer's own id, a partial under an id of its own.
        var fileId = Guid.TryParse(uploadId, out var uploadGuid) && uploadGuid == fileTransferId
            ? fileTransferId.ToString()
            : TusRouteHelper.NormalizePartialFileId(uploadId);
        return (await uploadProgressCache.GetAsync(fileId, cancellationToken))?.CommittedOffset ?? 0;
    }
}
