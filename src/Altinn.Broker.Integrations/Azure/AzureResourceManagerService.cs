﻿using System.Collections.Concurrent;

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
    private readonly IResourceOwnerRepository _resourceOwnerRepository;
    private readonly ConcurrentDictionary<string, (DateTime Created, string Token)> _sasTokens =
        new ConcurrentDictionary<string, (DateTime Created, string Token)>();
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly ILogger<AzureResourceManagerService> _logger;
    public string GetResourceGroupName(ResourceOwnerEntity resourceOwnerEntity) => $"serviceowner-{_resourceManagerOptions.Environment}-{resourceOwnerEntity.Id.Replace(":", "-")}-rg";
    public string GetStorageAccountName(ResourceOwnerEntity resourceOwnerEntity) => $"ai{_resourceManagerOptions.Environment.ToLowerInvariant()}{resourceOwnerEntity.Id.Replace(":", "")}sa";

    private SubscriptionResource GetSubscription() => _armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{_resourceManagerOptions.SubscriptionId}"));

    public AzureResourceManagerService(IOptions<AzureResourceManagerOptions> resourceManagerOptions, IHostEnvironment hostingEnvironment, IResourceOwnerRepository resourceOwnerRepository, ILogger<AzureResourceManagerService> logger)
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
        _resourceOwnerRepository = resourceOwnerRepository;
        _logger = logger;
    }

    public async Task Deploy(ResourceOwnerEntity resourceOwnerEntity, CancellationToken ct)
    {
        _logger.LogInformation($"Starting deployment for {resourceOwnerEntity.Name}");
        var resourceGroupName = GetResourceGroupName(resourceOwnerEntity);
        var storageAccountName = GetStorageAccountName(resourceOwnerEntity);

        _logger.LogInformation($"Resource group: {resourceGroupName}");
        _logger.LogInformation($"Storage account: {storageAccountName}");

        await _resourceOwnerRepository.InitializeStorageProvider(resourceOwnerEntity.Id, storageAccountName, StorageProviderType.Altinn3Azure);

        // Create or get the resource group
        var subscription = GetSubscription();
        var resourceGroupCollection = subscription.GetResourceGroups();
        var resourceGroupData = new ResourceGroupData(_resourceManagerOptions.Location);
        resourceGroupData.Tags.Add("customer_id", resourceOwnerEntity.Id);

        var resourceGroup = await resourceGroupCollection.CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, resourceGroupData, ct);

        // Create or get the storage account
        var storageAccountData = new StorageAccountCreateOrUpdateContent(new StorageSku(StorageSkuName.StandardLrs), StorageKind.StorageV2, new AzureLocation(_resourceManagerOptions.Location));
        storageAccountData.Tags.Add("customer_id", resourceOwnerEntity.Id);
        var storageAccountCollection = resourceGroup.Value.GetStorageAccounts();
        var storageAccount = await storageAccountCollection.CreateOrUpdateAsync(WaitUntil.Completed, storageAccountName, storageAccountData, ct);
        var blobService = storageAccount.Value.GetBlobService();
        string containerName = "brokerfiles";
        if (!blobService.GetBlobContainers().Any(container => container.Data.Name == containerName))
        {
            await blobService.GetBlobContainers().CreateOrUpdateAsync(WaitUntil.Completed, "brokerfiles", new BlobContainerData(), ct);
        }

        _logger.LogInformation($"Storage account {storageAccountName} created");
    }

    public async Task<DeploymentStatus> GetDeploymentStatus(ResourceOwnerEntity resourceOwnerEntity, CancellationToken ct)
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return DeploymentStatus.Ready;
        }
        var subscription = GetSubscription();
        _logger.LogInformation($"Looking up {GetResourceGroupName(resourceOwnerEntity)} in {subscription.Id}");
        var resourceGroupCollection = subscription.GetResourceGroups();
        var resourceGroupExists = await resourceGroupCollection.ExistsAsync(GetResourceGroupName(resourceOwnerEntity), ct);
        if (!resourceGroupExists)
        {
            _logger.LogInformation($"Could not find resource group for {resourceOwnerEntity.Name}");
            return DeploymentStatus.NotStarted;
        }

        var resourceGroup = await resourceGroupCollection.GetAsync(GetResourceGroupName(resourceOwnerEntity), ct);
        var storageAccountCollection = resourceGroup.Value.GetStorageAccounts();
        var storageAccountExists = await storageAccountCollection.ExistsAsync(GetStorageAccountName(resourceOwnerEntity), cancellationToken: ct);
        if (!storageAccountExists)
        {
            _logger.LogInformation($"Could not find storage account for {resourceOwnerEntity.Name}");
            return DeploymentStatus.DeployingResources;
        }

        return DeploymentStatus.Ready;
    }

    public async Task<string> GetStorageConnectionString(ResourceOwnerEntity resourceOwnerEntity)
    {
        _logger.LogInformation($"Retrieving connection string for {resourceOwnerEntity.Name}");
        var sasToken = await GetSasToken(resourceOwnerEntity);
        return $"BlobEndpoint=https://{GetStorageAccountName(resourceOwnerEntity)}.blob.core.windows.net/brokerfiles?{sasToken}";
    }


    private async Task<string> GetSasToken(ResourceOwnerEntity resourceOwnerEntity)
    {
        var storageAccountName = GetStorageAccountName(resourceOwnerEntity);
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
            newSasToken.Token = await CreateSasToken(resourceOwnerEntity);

            _sasTokens.TryAdd(storageAccountName, newSasToken);

            return newSasToken.Token;
        }
        finally
        {
            _semaphore.Release();
        }
    }
    private async Task<string> CreateSasToken(ResourceOwnerEntity resourceOwnerEntity)
    {
        _logger.LogInformation("Creating new SAS token for " + resourceOwnerEntity.Name);
        var resourceGroupName = GetResourceGroupName(resourceOwnerEntity);
        var storageAccountName = GetStorageAccountName(resourceOwnerEntity);
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
