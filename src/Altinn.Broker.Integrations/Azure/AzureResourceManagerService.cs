using System.Collections.Concurrent;

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
using Azure.Storage.Sas;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Azure;
public class AzureResourceManagerService : IResourceManager
{
    private readonly AzureResourceManagerOptions _resourceManagerOptions;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ArmClient _armClient;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly ConcurrentDictionary<string, (DateTime Created, string Token)> _sasTokens =
        new ConcurrentDictionary<string, (DateTime Created, string Token)>();
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly ILogger<AzureResourceManagerService> _logger;
    public string GetResourceGroupName(ServiceOwnerEntity serviceOwnerEntity) => $"serviceowner-{_resourceManagerOptions.Environment}-{serviceOwnerEntity.Id.Replace(":", "-")}-rg";
    public string GetStorageAccountName(ServiceOwnerEntity serviceOwnerEntity) => $"ai{_resourceManagerOptions.Environment.ToLowerInvariant()}{serviceOwnerEntity.Id.Replace(":", "")}sa";

    private SubscriptionResource GetSubscription() => _armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{_resourceManagerOptions.SubscriptionId}"));

    public AzureResourceManagerService(IOptions<AzureResourceManagerOptions> resourceManagerOptions, IHostEnvironment hostingEnvironment, IServiceOwnerRepository serviceOwnerRepository, ILogger<AzureResourceManagerService> logger)
    {
        _resourceManagerOptions = resourceManagerOptions.Value;
        _hostEnvironment = hostingEnvironment;
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

    public async Task Deploy(ServiceOwnerEntity serviceOwnerEntity, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Starting deployment for {serviceOwnerEntity.Name}");
        var resourceGroupName = GetResourceGroupName(serviceOwnerEntity);
        var storageAccountName = GetStorageAccountName(serviceOwnerEntity);

        _logger.LogInformation($"Resource group: {resourceGroupName}");
        _logger.LogInformation($"Storage account: {storageAccountName}");

        await _serviceOwnerRepository.InitializeStorageProvider(serviceOwnerEntity.Id, storageAccountName, StorageProviderType.Altinn3Azure);

        // Create or get the resource group
        var subscription = GetSubscription();
        var resourceGroupCollection = subscription.GetResourceGroups();
        var resourceGroupData = new ResourceGroupData(_resourceManagerOptions.Location);
        resourceGroupData.Tags.Add("customer_id", serviceOwnerEntity.Id);

        var resourceGroup = await resourceGroupCollection.CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, resourceGroupData, cancellationToken);

        // Create or get the storage account
        var storageAccountData = new StorageAccountCreateOrUpdateContent(new StorageSku(StorageSkuName.StandardLrs), StorageKind.StorageV2, new AzureLocation(_resourceManagerOptions.Location));
        storageAccountData.Tags.Add("customer_id", serviceOwnerEntity.Id);
        var storageAccountCollection = resourceGroup.Value.GetStorageAccounts();
        var storageAccount = await storageAccountCollection.CreateOrUpdateAsync(WaitUntil.Completed, storageAccountName, storageAccountData, cancellationToken);
        var blobService = storageAccount.Value.GetBlobService();
        string containerName = "brokerfiles";
        if (!blobService.GetBlobContainers().Any(container => container.Data.Name == containerName))
        {
            await blobService.GetBlobContainers().CreateOrUpdateAsync(WaitUntil.Completed, "brokerfiles", new BlobContainerData(), cancellationToken);
        }

        _logger.LogInformation($"Storage account {storageAccountName} created");
    }

    public async Task<DeploymentStatus> GetDeploymentStatus(ServiceOwnerEntity serviceOwnerEntity, CancellationToken cancellationToken)
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return DeploymentStatus.Ready;
        }
        var subscription = GetSubscription();
        _logger.LogInformation($"Looking up {GetResourceGroupName(serviceOwnerEntity)} in {subscription.Id}");
        var resourceGroupCollection = subscription.GetResourceGroups();
        var resourceGroupExists = await resourceGroupCollection.ExistsAsync(GetResourceGroupName(serviceOwnerEntity), cancellationToken);
        if (!resourceGroupExists)
        {
            _logger.LogInformation($"Could not find resource group for {serviceOwnerEntity.Name}");
            return DeploymentStatus.NotStarted;
        }

        var resourceGroup = await resourceGroupCollection.GetAsync(GetResourceGroupName(serviceOwnerEntity), cancellationToken);
        var storageAccountCollection = resourceGroup.Value.GetStorageAccounts();
        var storageAccountExists = await storageAccountCollection.ExistsAsync(GetStorageAccountName(serviceOwnerEntity), cancellationToken: cancellationToken);
        if (!storageAccountExists)
        {
            _logger.LogInformation($"Could not find storage account for {serviceOwnerEntity.Name}");
            return DeploymentStatus.DeployingResources;
        }

        return DeploymentStatus.Ready;
    }

    public async Task<string> GetStorageConnectionString(ServiceOwnerEntity serviceOwnerEntity)
    {
        _logger.LogInformation($"Retrieving connection string for {serviceOwnerEntity.Name}");
        var sasToken = await GetSasToken(serviceOwnerEntity);
        return $"BlobEndpoint=https://{GetStorageAccountName(serviceOwnerEntity)}.blob.core.windows.net/brokerfiles?{sasToken}";
    }


    private async Task<string> GetSasToken(ServiceOwnerEntity serviceOwnerEntity)
    {
        var storageAccountName = GetStorageAccountName(serviceOwnerEntity);
        if (_sasTokens.TryGetValue(storageAccountName, out (DateTime Created, string Token) sasToken) && sasToken.Created.AddHours(8) > DateTime.UtcNow)
        {
            return sasToken.Token;
        }

        _sasTokens.TryRemove(storageAccountName, out _);

        await _semaphore.WaitAsync();
        try
        {
            if (_sasTokens.TryGetValue(storageAccountName, out sasToken))
            {
                return sasToken.Token;
            }
            (DateTime Created, string Token) newSasToken = default;
            newSasToken.Created = DateTime.UtcNow;
            newSasToken.Token = await CreateSasToken(serviceOwnerEntity);

            _sasTokens.TryAdd(storageAccountName, newSasToken);

            return newSasToken.Token;
        }
        finally
        {
            _semaphore.Release();
        }
    }
    private async Task<string> CreateSasToken(ServiceOwnerEntity serviceOwnerEntity)
    {
        _logger.LogInformation("Creating new SAS token for " + serviceOwnerEntity.Name);
        var resourceGroupName = GetResourceGroupName(serviceOwnerEntity);
        var storageAccountName = GetStorageAccountName(serviceOwnerEntity);
        var subscription = GetSubscription();
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
        var containerName = "brokerfiles";
        BlobSasBuilder sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = containerName,
            Resource = "c",
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(24),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Create | BlobSasPermissions.List | BlobSasPermissions.Write);
        string sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();
        return sasToken;
    }
}
