
using System.Numerics;

using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IResourceRepository
{
    Task<ResourceEntity?> GetResource(string resourceId, CancellationToken cancellationToken = default);
    Task UpdateMaxFileTransferSize(string resourceId, long maxSize, CancellationToken cancellationToken = default);
    Task CreateResource(ResourceEntity resource, CancellationToken cancellationToken = default);
}
