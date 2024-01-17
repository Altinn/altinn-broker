
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IResourceRepository
{
    Task<ResourceEntity?> GetResource(long id);
    Task<ResourceEntity?> GetResource(string clientId);
    Task<List<string>> SearchResources(string resourceOwnerOrgNo);
    Task<long> InitializeResource(string resourceOwnerId, string organizationNumber, string resourceId);
}
