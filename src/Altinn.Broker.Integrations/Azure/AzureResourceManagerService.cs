using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Azure.Storage;
using Azure.Storage.Sas;

using Hangfire;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Azure;
public class AzureResourceManagerService : IResourceManager
{
    private readonly AzureResourceManagerOptions _resourceManagerOptions;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ArmClient _armClient;
    private readonly ConcurrentDictionary<string, (DateTime Created, string Token)> _sasTokens =
        new ConcurrentDictionary<string, (DateTime Created, string Token)>();
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly TokenCredential _credentials;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<AzureResourceManagerService> _logger;
    private string GetResourceGroupName(string serviceOwnerId) => $"serviceowner-{_resourceManagerOptions.Environment}-{serviceOwnerId.Replace(":", "-")}-rg";
    private string? GetStorageAccountName(StorageProviderEntity storageProviderEntity) => storageProviderEntity.ResourceName;

    private SubscriptionResource GetSubscription() => _armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{_resourceManagerOptions.SubscriptionId}"));

    public AzureResourceManagerService(IOptions<AzureResourceManagerOptions> resourceManagerOptions, IServiceOwnerRepository serviceOwnerRepository, IHostEnvironment hostingEnvironment, IBackgroundJobClient backgroundJobClient, ILogger<AzureResourceManagerService> logger)
    {
        _resourceManagerOptions = resourceManagerOptions.Value;
        _hostEnvironment = hostingEnvironment;
        _credentials = new DefaultAzureCredential();
        _armClient = new ArmClient(_credentials);
        _serviceOwnerRepository = serviceOwnerRepository;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public void CreateStorageProviders(ServiceOwnerEntity serviceOwnerEntity, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating storage providers for {serviceOwnerEntity.Name}");
        if (_hostEnvironment.IsDevelopment())
        {
            _backgroundJobClient.Enqueue<IServiceOwnerRepository>(service => service.InitializeStorageProvider(serviceOwnerEntity.Id, "Dummy value", StorageProviderType.Altinn3Azure));
        } 
        else
        {
            var virusScanStorageProviderJob = _backgroundJobClient.Enqueue<IResourceManager>(service => service.Deploy(serviceOwnerEntity, true, cancellationToken));
            _backgroundJobClient.ContinueJobWith<IResourceManager>(virusScanStorageProviderJob, service => service.Deploy(serviceOwnerEntity, false, cancellationToken));
        }
    }

    public async Task Deploy(ServiceOwnerEntity serviceOwnerEntity, bool virusScan, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Starting deployment for {serviceOwnerEntity.Name}");
        _logger.LogInformation($"Using app identity for deploying Azure resources"); // TODO remove
        var resourceGroupName = GetResourceGroupName(serviceOwnerEntity.Id);

        var storageAccountName = GenerateStorageAccountName();
        _logger.LogInformation($"Resource group: {resourceGroupName}");
        _logger.LogInformation($"Storage account: {storageAccountName}");

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
        if (virusScan) { 
            await EnableMicrosoftDefender(resourceGroupName, storageAccountName, cancellationToken);
        }
        var blobService = storageAccount.Value.GetBlobService();
        string containerName = "brokerfiles";
        if (!blobService.GetBlobContainers().Any(container => container.Data.Name == containerName))
        {
            await blobService.GetBlobContainers().CreateOrUpdateAsync(WaitUntil.Completed, containerName, new BlobContainerData(), cancellationToken);
        }

        await _serviceOwnerRepository.InitializeStorageProvider(serviceOwnerEntity.Id, storageAccountName, virusScan ? StorageProviderType.Altinn3Azure : StorageProviderType.Altinn3AzureWithoutVirusScan);
        _logger.LogInformation($"Storage account {storageAccountName} created");
    }

    private async Task EnableMicrosoftDefender(string resourceGroupName, string storageAccountName, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
        var token = await _credentials.GetTokenAsync(tokenRequestContext, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        var endpoint = $"https://management.azure.com/subscriptions/{_resourceManagerOptions.SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{storageAccountName}/providers/Microsoft.Security/defenderForStorageSettings/current?api-version=2022-12-01-preview";
        var requestBody = new MalwareScanConfiguration()
        {
            Properties = new Properties()
            {
                IsEnabled = true,
                MalwareScanning = new MalwareScanning()
                {
                    OnUpload = new OnUpload()
                    {
                        IsEnabled = true,
                        CapGBPerMonth = 5000
                    },
                    ScanResultsEventGridTopicResourceId = $"/subscriptions/{_resourceManagerOptions.SubscriptionId}/resourceGroups/{_resourceManagerOptions.ApplicationResourceGroupName}/providers/Microsoft.EventGrid/topics/{_resourceManagerOptions.MalwareScanEventGridTopicName}"
                },
                OverrideSubscriptionLevelSettings = true,
                SensitiveDataDiscovery = new SensitiveDataDiscovery()
                {
                    IsEnabled = false
                }
            },
            Scope = $"[resourceId('Microsoft.Storage/storageAccounts', parameters({storageAccountName}))]"
        };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PutAsync(endpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to enable Defender Malware Scan. Error: {error}", errorMessage);
            throw new HttpRequestException($"Failed to enable Defender Malware Scan. Error: {errorMessage}");
        }
        _logger.LogInformation($"Microsoft Defender Malware scan enabled for storage account {storageAccountName}");
        client.Dispose();
    }

    private string GenerateStorageAccountName()
    {
        Random random = new Random();
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var obfuscationString = new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
        return "aibroker" + obfuscationString + "sa";
    }

    public async Task<DeploymentStatus> GetDeploymentStatus(StorageProviderEntity storageProvider, CancellationToken cancellationToken)
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return DeploymentStatus.Ready;
        }
        var subscription = GetSubscription();
        _logger.LogInformation($"Looking up {GetResourceGroupName(storageProvider.ServiceOwnerId)} in {subscription.Id}");
        var resourceGroupCollection = subscription.GetResourceGroups();
        var resourceGroupExists = await resourceGroupCollection.ExistsAsync(GetResourceGroupName(storageProvider.ServiceOwnerId), cancellationToken);
        if (!resourceGroupExists)
        {
            _logger.LogInformation($"Could not find resource group for {storageProvider.ServiceOwnerId}");
            return DeploymentStatus.NotStarted;
        }

        var resourceGroup = await resourceGroupCollection.GetAsync(GetResourceGroupName(storageProvider.ServiceOwnerId), cancellationToken);
        var storageAccountCollection = resourceGroup.Value.GetStorageAccounts();
        var storageAccountName = GetStorageAccountName(storageProvider);
        var storageAccountExists = storageAccountName != null && await storageAccountCollection.ExistsAsync(storageAccountName, cancellationToken: cancellationToken);
        if (!storageAccountExists)
        {
            _logger.LogInformation($"Could not find storage account for {storageProvider.ServiceOwnerId}");
            return DeploymentStatus.DeployingResources;
        }

        return DeploymentStatus.Ready;
    }

    public async Task<string> GetStorageConnectionString(StorageProviderEntity storageProviderEntity)
    {
        _logger.LogInformation($"Retrieving connection string for storage provider {storageProviderEntity.Id}");
        if (_hostEnvironment.IsDevelopment())
        {
            return AzureConstants.AzuriteUrl;
        }
        if (storageProviderEntity.ResourceName == null)
        {
            throw new InvalidOperationException("Storage account has not been deployed");
        }
        var sasToken = await GetSasToken(storageProviderEntity, storageProviderEntity.ResourceName);
        return $"BlobEndpoint=https://{storageProviderEntity.ResourceName}.blob.core.windows.net/brokerfiles?{sasToken}";
    }


    private async Task<string> GetSasToken(StorageProviderEntity storageProviderEntity, string storageAccountName)
    {
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
            newSasToken.Token = await CreateSasToken(storageProviderEntity, storageAccountName);

            _sasTokens.TryAdd(storageAccountName, newSasToken);

            return newSasToken.Token;
        }
        finally
        {
            _semaphore.Release();
        }
    }
    private async Task<string> CreateSasToken(StorageProviderEntity storageProviderEntity, string storageAccountName)
    {
        _logger.LogInformation($"Creating new SAS token for {storageProviderEntity.ServiceOwnerId}: {storageProviderEntity.ResourceName}");
        var resourceGroupName = GetResourceGroupName(storageProviderEntity.ServiceOwnerId);
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
        sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Create | BlobSasPermissions.List | BlobSasPermissions.Write | BlobSasPermissions.Delete);
        string sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();
        _logger.LogInformation("SAS Token created");
        return sasToken;
    }

    public async Task UpdateContainerAppIpRestrictionsAsync(List<string> newIps, CancellationToken cancellationToken)
    {
        var containerAppResourceId = new ResourceIdentifier($"/subscriptions/{_resourceManagerOptions.SubscriptionId}/resourceGroups/{_resourceManagerOptions.ApplicationResourceGroupName}/providers/Microsoft.App/containerapps/{_resourceManagerOptions.ContainerAppName}");
        var containerApp = await _armClient.GetContainerAppResource(containerAppResourceId).GetAsync(cancellationToken);

        var currentIpRestrictions = containerApp.Value.Data.Configuration.Ingress.IPSecurityRestrictions;

        currentIpRestrictions.Clear();

        foreach (var ip in newIps)
        {
            currentIpRestrictions.Add(new ContainerAppIPSecurityRestrictionRule(name: $"IP whitelist {ip}", action: ContainerAppIPRuleAction.Allow, ipAddressRange: ip));
        }

        await containerApp.Value.UpdateAsync(waitUntil: WaitUntil.Started, data: containerApp.Value.Data, cancellationToken: cancellationToken);

        return;
    }

    public async Task<ServiceTagsListResult?> RetrieveServiceTags(CancellationToken cancellationToken)
    {
        Response<ServiceTagsListResult> response = await GetSubscription().GetServiceTagAsync(_resourceManagerOptions.Location, cancellationToken);
        if (response.GetRawResponse().Status != 200)
        {
            _logger.LogError("Failed to retrieve Azure service tags in Azure Resource Manager Service. Status code: {StatusCode}", response.GetRawResponse().Status);
            return null;
        }
        return response.Value;
    }

    public async Task<List<string>> RetrieveCurrentIpRanges(CancellationToken cancellationToken)
    {
        var serviceTagsListResult = await RetrieveServiceTags(cancellationToken);
        var retrievedAddresses = serviceTagsListResult?.Values
            .Where(v => string.Equals(v.Id, $"AzureEventGrid.{_resourceManagerOptions.Location}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(v => v.Properties.AddressPrefixes)
            .ToList();
        if (retrievedAddresses == null || retrievedAddresses.Count == 0)
        {
            _logger.LogError($"No EventGrid IP addresses were retrieved. Service tag 'AzureEventGrid.{_resourceManagerOptions.Location}' may not exist.");
            return new List<string>();
        }
        retrievedAddresses.Add(_resourceManagerOptions.ApimIP);
        return retrievedAddresses;
    }
}
