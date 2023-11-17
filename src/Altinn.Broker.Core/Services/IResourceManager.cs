using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Services;
public interface IResourceManager
{
    Task<DeploymentStatus> GetDeploymentStatus(ServiceOwnerEntity serviceOwnerEntity);

    /// <summary>
    /// Deploys the required resources for the ServiceOwner. Must be idempotent.
    /// </summary>
    /// <param name="serviceOwnerEntity"></param>
    /// <returns></returns>
    Task Deploy(ServiceOwnerEntity serviceOwnerEntity);

    Task<string> GetStorageConnectionString(ServiceOwnerEntity? serviceOwnerEntity);

    string GetResourceGroupName(ServiceOwnerEntity serviceOwnerEntity);

    string GetStorageAccountName(ServiceOwnerEntity serviceOwnerEntity);
}
