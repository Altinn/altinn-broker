namespace Altinn.Broker.Application.UploadFile.Tus;

public interface ITusUploadFinalizationProgressService
{
    Task<bool> IsTusFinalizationInProgressAsync(Guid fileTransferId, CancellationToken cancellationToken);
}
