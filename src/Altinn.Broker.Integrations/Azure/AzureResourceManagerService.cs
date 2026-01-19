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
        storageAccountData.MinimumTlsVersion = "TLS1_2";
        storageAccountData.AllowSharedKeyAccess = false;
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
        return $"BlobEndpoint=https://{storageProviderEntity.ResourceName}.blob.core.windows.net";
    }

    public async Task UpdateContainerAppIpRestrictionsAsync(Dictionary<string, string> newIps, CancellationToken cancellationToken)
    {
        try 
        {
            var containerAppResourceId = new ResourceIdentifier($"/subscriptions/{_resourceManagerOptions.SubscriptionId}/resourceGroups/{_resourceManagerOptions.ApplicationResourceGroupName}/providers/Microsoft.App/containerapps/{_resourceManagerOptions.ContainerAppName}");
            var containerApp = await _armClient.GetContainerAppResource(containerAppResourceId).GetAsync(cancellationToken);

            var ipRestrictions = containerApp.Value.Data.Configuration.Ingress.IPSecurityRestrictions;

            ipRestrictions.Clear();

            foreach (var ip in newIps)
            {
                ipRestrictions.Add(new ContainerAppIPSecurityRestrictionRule(name: $"IP whitelist {ip.Value}", action: ContainerAppIPRuleAction.Allow, ipAddressRange: ip.Key));
            }

            _logger.LogInformation("Updating IP restrictions for container app");
            var response = await containerApp.Value.UpdateAsync(waitUntil: WaitUntil.Started, data: containerApp.Value.Data, cancellationToken: cancellationToken);

            if (response.GetRawResponse().Status != 200)
            {
                _logger.LogError("Failed to update IP restrictions for container app. Status code: {StatusCode}", response.GetRawResponse().Status);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception occurred while updating IP security restrictions for container app");
        }
    }

    public async Task<ServiceTagsListResult?> RetrieveServiceTags(CancellationToken cancellationToken)
    {
        try
        {
            Response<ServiceTagsListResult> response = await GetSubscription().GetServiceTagAsync(_resourceManagerOptions.Location, cancellationToken);
            if (response.GetRawResponse().Status != 200)
            {
                _logger.LogError("Failed to retrieve Azure service tags in Azure Resource Manager Service. Status code: {StatusCode}", response.GetRawResponse().Status);
                return null;
            }
            return response.Value;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception occurred while retrieving Azure service tags");
            return null;
        }
    }

    public async Task<Dictionary<string, string>> RetrieveCurrentIpRanges(CancellationToken cancellationToken)
    {
        string serviceTagId = "AzureEventGrid";
        var serviceTagsListResult = await RetrieveServiceTags(cancellationToken);
        var retrievedAddresses = serviceTagsListResult?.Values
            .Where(v => string.Equals(v.Id, serviceTagId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(v => v.Properties.AddressPrefixes)
            .Where(ip => !ip.Contains(':'))
            .ToList();
        if (retrievedAddresses == null || retrievedAddresses.Count == 0)
        {
            _logger.LogError($"No EventGrid IP addresses were retrieved. Service tag '{serviceTagId}' may not exist.");
            return new Dictionary<string, string>();
        }
        Dictionary<string, string> addresses = retrievedAddresses.ToDictionary(ip => ip, ip => $"{serviceTagId} IP");
        addresses.Add(_resourceManagerOptions.ApimIP, "Apim IP");
        return addresses;
    }
}
