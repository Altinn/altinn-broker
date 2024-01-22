
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IResourceRepository
{
    Task<ResourceEntity?> GetResource(string resourceId);
    Task<List<string>> SearchResources(string resourceOwnerOrgNo);
    Task<long> InitializeResource(string resourceOwnerId, string organizationNumber, string resourceId);
}
