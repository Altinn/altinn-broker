namespace Altinn.Broker.Application.UploadPlans;

/// <summary>
/// How far tus uploads got, for showing progress. It reads what the server recorded rather than
/// asking tus, since a tus request on an upload takes it over from the client sending to it.
/// </summary>
public interface ITusUploadProgressReader
{
    Task<long> GetUploadedBytesAsync(Guid fileTransferId, string uploadId, CancellationToken cancellationToken);
}
