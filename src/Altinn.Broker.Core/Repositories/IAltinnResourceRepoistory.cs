using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IAltinnResourceRepository
{
    Task<ResourceEntity?> GetResource(string resourceId, CancellationToken cancellationToken = default);
}
