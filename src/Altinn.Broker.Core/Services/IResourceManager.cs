using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Services;
public interface IResourceManager
{
    Task GetDeploymentStatus(ServiceOwnerEntity serviceOwnerEntity);

    /// <summary>
    /// Deploys the required resources for the ServiceOwner. Must be idempotent.
    /// </summary>
    /// <param name="serviceOwnerEntity"></param>
    /// <returns></returns>
    Task Deploy(ServiceOwnerEntity serviceOwnerEntity);
}
