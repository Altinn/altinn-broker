namespace Altinn.Broker.Application.UploadFile;

public interface ITusUploadFinalizationProgressService
{
    Task<bool> IsTusFinalizationInProgressAsync(Guid fileTransferId, CancellationToken cancellationToken);
}
