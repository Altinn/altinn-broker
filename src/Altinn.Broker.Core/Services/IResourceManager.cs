using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Services;
public interface IResourceManager
{
    Task<DeploymentStatus> GetDeploymentStatus(ResourceOwnerEntity resourceOwnerEntity);

    /// <summary>
    /// Deploys the required resources for the ResourceOwner. Must be idempotent.
    /// </summary>
    /// <param name="resourceOwnerEntity"></param>
    /// <returns></returns>
    Task Deploy(ResourceOwnerEntity resourceOwnerEntity);

    Task<string> GetStorageConnectionString(ResourceOwnerEntity resourceOwnerEntity);

    string GetResourceGroupName(ResourceOwnerEntity resourceOwnerEntity);

    string GetStorageAccountName(ResourceOwnerEntity resourceOwnerEntity);
}
