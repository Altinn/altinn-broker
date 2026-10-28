namespace Altinn.Broker.Application.UploadFile.Tus;

public interface ITusPartialUploadLookup
{
    Task<IReadOnlyList<TusPartialUpload>> GetPartialUploadsAsync(Guid fileTransferId, CancellationToken cancellationToken);
}

public sealed record TusPartialUpload(string PartialUploadId, long UploadLength);
