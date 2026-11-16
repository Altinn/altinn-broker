namespace Altinn.Broker.Application.UploadPlans;

public interface IUploadPlanStore
{
    Task<UploadPlan?> TryGetAsync(Guid fileTransferId, CancellationToken cancellationToken);

    Task SaveAsync(Guid fileTransferId, UploadPlan plan, CancellationToken cancellationToken);

    Task RemoveAsync(Guid fileTransferId, CancellationToken cancellationToken);
}
