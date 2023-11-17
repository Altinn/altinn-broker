using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Services;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;

using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Azure;
public class AzureResourceManager : IResourceManager
{
    private readonly AzureResourceManagerOptions _resourceManagerOptions;
    private readonly AzureStorageOptions _storageOptions;
    private readonly ArmClient _armClient;
    public string GetResourceGroupName(ServiceOwnerEntity serviceOwnerEntity) => $"serviceowner-{serviceOwnerEntity.Id.Replace(":", "-")}-rg";
    public string GetStorageAccountName(ServiceOwnerEntity serviceOwnerEntity) => $"serviceowner{serviceOwnerEntity.Id.Replace(":", "-")}sa";

    public AzureResourceManager(IOptions<AzureResourceManagerOptions> resourceManagerOptions, IOptions<AzureStorageOptions> storageOptions)
    {
        _resourceManagerOptions = resourceManagerOptions.Value;
        _storageOptions = storageOptions.Value;
        //var credentials = new ClientSecretCredential(_resourceManagerOptions.TenantId, _resourceManagerOptions.ClientId, _resourceManagerOptions.ClientSecret);
        _armClient = new ArmClient(new DefaultAzureCredential());
    }

    public async Task Deploy(ServiceOwnerEntity serviceOwnerEntity)
    {
        var resourceGroupName = GetResourceGroupName(serviceOwnerEntity);
        var storageAccountName = GetStorageAccountName(serviceOwnerEntity);

        // Create or get the resource group
        var subscriptionIdentifier = new ResourceIdentifier(_resourceManagerOptions.SubscriptionId);
        var resourceGroupCollection = _armClient.GetSubscriptionResource(subscriptionIdentifier).GetResourceGroups();
        var resourceGroupData = new ResourceGroupData(_resourceManagerOptions.Location);       
        var resourceGroup = await resourceGroupCollection.CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, resourceGroupData);

        // Create or get the storage account
        var storageSku = new StorageSku(StorageSkuName.StandardLrs);
        
        var storageAccountData = new StorageAccountCreateOrUpdateContent(storageSku, StorageKind.StorageV2, new AzureLocation(_resourceManagerOptions.Location));
        var storageAccountCollection = resourceGroup.Value.GetStorageAccounts();
        await storageAccountCollection.CreateOrUpdateAsync(WaitUntil.Completed, storageAccountName, storageAccountData);
    }

    public async Task<DeploymentStatus> GetDeploymentStatus(ServiceOwnerEntity serviceOwnerEntity)
    {
        var subscriptionIdentifier = new ResourceIdentifier(_resourceManagerOptions.SubscriptionId);
        var resourceGroupCollection = _armClient.GetSubscriptionResource(subscriptionIdentifier).GetResourceGroups();
        var resourceGroupExists = await resourceGroupCollection.ExistsAsync(GetResourceGroupName(serviceOwnerEntity));
        if (!resourceGroupExists)
        {
            return DeploymentStatus.NotStarted;
        }

        var resourceGroup = await resourceGroupCollection.GetAsync(GetResourceGroupName(serviceOwnerEntity));
        var storageAccountCollection = resourceGroup.Value.GetStorageAccounts();
        var storageAccountExists = await storageAccountCollection.ExistsAsync(GetStorageAccountName(serviceOwnerEntity));
        if (!storageAccountExists)
        {
            return DeploymentStatus.DeployingResources;
        }

        return DeploymentStatus.Ready;
    }

    public async Task<string> GetStorageConnectionString(ServiceOwnerEntity? serviceOwnerEntity)
    {
        if (serviceOwnerEntity is null)
        {
            return _storageOptions.ConnectionString;
        }
        throw new NotImplementedException();
    }
}
