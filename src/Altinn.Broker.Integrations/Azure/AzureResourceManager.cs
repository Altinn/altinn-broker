using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Azure;
public class AzureResourceManager : IResourceManager
{
    private readonly AzureResourceManagerOptions _resourceManagerOptions;
    private readonly IHostingEnvironment _hostingEnvironment;
    private readonly ArmClient _armClient;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly ILogger<AzureResourceManager> _logger;
    public string GetResourceGroupName(ServiceOwnerEntity serviceOwnerEntity) => $"serviceowner-{_resourceManagerOptions.Environment}-{serviceOwnerEntity.Id.Replace(":", "-")}-rg";
    public string GetStorageAccountName(ServiceOwnerEntity serviceOwnerEntity) => $"ai{_resourceManagerOptions.Environment.ToLowerInvariant()}{serviceOwnerEntity.Id.Replace(":", "")}sa";

    public AzureResourceManager(IOptions<AzureResourceManagerOptions> resourceManagerOptions, IHostingEnvironment hostingEnvironment, IServiceOwnerRepository serviceOwnerRepository, ILogger<AzureResourceManager> logger)
    {
        _resourceManagerOptions = resourceManagerOptions.Value;
        _hostingEnvironment = hostingEnvironment;
        if (string.IsNullOrWhiteSpace(_resourceManagerOptions.ClientId))
        {
            _armClient = new ArmClient(new DefaultAzureCredential());
        }
        else
        {
            var credentials = new ClientSecretCredential(_resourceManagerOptions.TenantId, _resourceManagerOptions.ClientId, _resourceManagerOptions.ClientSecret);
            _armClient = new ArmClient(credentials);
        }
        _serviceOwnerRepository = serviceOwnerRepository;
        _logger = logger;
    }

    public async Task Deploy(ServiceOwnerEntity serviceOwnerEntity)
    {
        _logger.LogInformation($"Starting deployment for {serviceOwnerEntity.Name}");
        var resourceGroupName = GetResourceGroupName(serviceOwnerEntity);
        var storageAccountName = GetStorageAccountName(serviceOwnerEntity);

        _logger.LogInformation($"Resource group: {resourceGroupName}");
        _logger.LogInformation($"Storage account: {storageAccountName}");

        await _serviceOwnerRepository.InitializeStorageProvider(serviceOwnerEntity.Id, storageAccountName, StorageProviderType.Altinn3Azure);

        // Create or get the resource group
        var subscription = await _armClient.GetDefaultSubscriptionAsync();
        var resourceGroupCollection = subscription.GetResourceGroups();
        var resourceGroupData = new ResourceGroupData(_resourceManagerOptions.Location);
        resourceGroupData.Tags.Add("customer_id", serviceOwnerEntity.Id);

        var resourceGroup = await resourceGroupCollection.CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, resourceGroupData);

        // Create or get the storage account
        var storageAccountData = new StorageAccountCreateOrUpdateContent(new StorageSku(StorageSkuName.StandardLrs), StorageKind.StorageV2, new AzureLocation(_resourceManagerOptions.Location));
        storageAccountData.Tags.Add("customer_id", serviceOwnerEntity.Id);
        var storageAccountCollection = resourceGroup.Value.GetStorageAccounts();
        var storageAccount = await storageAccountCollection.CreateOrUpdateAsync(WaitUntil.Completed, storageAccountName, storageAccountData);
        var blobService = storageAccount.Value.GetBlobService();
        string containerName = "brokerfiles";
        if (!blobService.GetBlobContainers().Any(container => container.Data.Name == containerName))
        {
            await blobService.GetBlobContainers().CreateOrUpdateAsync(WaitUntil.Completed, "brokerfiles", new BlobContainerData());
        }

        _logger.LogInformation($"Storage account {storageAccountName} created");
    }

    public async Task<DeploymentStatus> GetDeploymentStatus(ServiceOwnerEntity serviceOwnerEntity)
    {
        if (_hostingEnvironment.IsDevelopment())
        {
            return DeploymentStatus.Ready;
        }
        var resourceGroupCollection = _armClient.GetDefaultSubscription().GetResourceGroups();
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

    public async Task<string> GetStorageConnectionString(ServiceOwnerEntity serviceOwnerEntity)
    {
        var resourceGroupName = GetResourceGroupName(serviceOwnerEntity);
        var storageAccountName = GetStorageAccountName(serviceOwnerEntity);
        var subscription = await _armClient.GetDefaultSubscriptionAsync();
        var resourceGroupCollection = subscription.GetResourceGroups();
        var resourceGroup = await resourceGroupCollection.GetAsync(resourceGroupName);
        var storageAccountCollection = resourceGroup.Value.GetStorageAccounts();
        var storageAccount = await storageAccountCollection.GetAsync(storageAccountName);
        string accountKey = "";
        var keys = storageAccount.Value.GetKeysAsync();
        await using (var keyEnumerator = keys.GetAsyncEnumerator())
        {
            accountKey = await keyEnumerator.MoveNextAsync() ? keyEnumerator.Current.Value : "";
        }
        StorageSharedKeyCredential credential = new StorageSharedKeyCredential(storageAccountName, accountKey);
        BlobServiceClient serviceClient = new BlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"), credential);
        var containerName = "brokerfiles";
        BlobSasBuilder sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = containerName,
            Resource = "c",
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Create | BlobSasPermissions.List | BlobSasPermissions.Write);
        string sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();

        return $"{serviceClient.Uri}{sasBuilder.BlobContainerName}?{sasToken}";
    }
}
