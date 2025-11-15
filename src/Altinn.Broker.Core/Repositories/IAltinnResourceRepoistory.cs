using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IAltinnResourceRepository
{
    Task<ResourceEntity?> GetResourceEntity(string resourceId, CancellationToken cancellationToken = default);
    Task<string?> GetServiceOwnerNameOfResource(string resourceId, CancellationToken cancellationToken = default);
}
