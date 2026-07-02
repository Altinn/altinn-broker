using System.Collections.Concurrent;

using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Integrations.Azure;

using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Blobs.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

using Xtensible.TusDotNet.Azure;

namespace Altinn.Broker.Integrations.Tus;

public interface ITusStorageResolver
{
    Task<AzureBlobTusStore?> GetStoreForFileAsync(string fileId, CancellationToken cancellationToken);
    Task SetStagingBlobMd5ChecksumAsync(string fileId, byte[] md5Hash, CancellationToken cancellationToken);
    Task StageTusBlockAsync(string fileId, string blockId, byte[] blockData, CancellationToken cancellationToken);
    Task CommitTusBlocksAsync(
        string fileId,
        IReadOnlyList<string> blockIds,
        byte[] md5Hash,
        CancellationToken cancellationToken);
}

public class TusStorageResolver(
    IFileTransferRepository fileTransferRepository,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IHostEnvironment hostEnvironment,
    ITusExpirationDetailsStore expirationDetailsStore,
    IHttpContextAccessor httpContextAccessor) : ITusStorageResolver
{
    private readonly ConcurrentDictionary<string, AzureBlobTusStore> _stores = new(StringComparer.OrdinalIgnoreCase);

    public async Task<AzureBlobTusStore?> GetStoreForFileAsync(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return null;
        }

        return _stores.GetOrAdd(storageContext.StorageAccountName, _ => new AzureBlobTusStore(
            storageContext.ConnectionString,
            AzureStorageConstants.BrokerFilesContainerName,
            new AzureBlobTusStoreOptions
            {
                BlobPath = AzureStorageConstants.TusStagingBlobPath,
                AuthenticationMode = storageContext.AuthenticationMode,
                ExpirationDetailsStore = expirationDetailsStore,
                FileIdGeneratorAsync = _ =>
                {
                    var httpContext = httpContextAccessor.HttpContext
                        ?? throw new InvalidOperationException("Missing HTTP context");
                    if (!TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var fileTransferId))
                    {
                        throw new InvalidOperationException("Missing file transfer id in route");
                    }

                    return Task.FromResult(fileTransferId.ToString());
                }
            }));
    }

    public async Task SetStagingBlobMd5ChecksumAsync(string fileId, byte[] md5Hash, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return;
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var appendBlobClient = containerClient.GetAppendBlobClient(
            Path.Combine(AzureStorageConstants.TusStagingBlobPath, fileId));
        var properties = await appendBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var metadata = properties.Value.Metadata.ToDictionary(static k => k.Key, static v => v.Value);
        metadata[AzureStorageConstants.TusMd5ChecksumMetadataKey] = Convert.ToBase64String(md5Hash);
        await appendBlobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
    }

    public async Task StageTusBlockAsync(string fileId, string blockId, byte[] blockData, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        await using var chunkStream = new MemoryStream(blockData, writable: false);
        await blockBlobClient.StageBlockAsync(
            blockId,
            chunkStream,
            cancellationToken: cancellationToken);
    }

    public async Task CommitTusBlocksAsync(
        string fileId,
        IReadOnlyList<string> blockIds,
        byte[] md5Hash,
        CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        await blockBlobClient.CommitBlockListAsync(
            blockIds,
            new CommitBlockListOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    [AzureStorageConstants.TusMd5ChecksumMetadataKey] = Convert.ToBase64String(md5Hash)
                },
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/octet-stream",
                    ContentHash = md5Hash
                }
            },
            cancellationToken: cancellationToken);
    }

    private BlobContainerClient GetBlobContainerClient(TusStorageContext storageContext)
    {
        if (hostEnvironment.IsDevelopment())
        {
            return new BlobServiceClient(AzureConstants.AzuriteUrl)
                .GetBlobContainerClient(AzureStorageConstants.BrokerFilesContainerName);
        }

        var storageUri = new Uri(storageContext.ConnectionString);
        return new BlobServiceClient(storageUri, new DefaultAzureCredential())
            .GetBlobContainerClient(AzureStorageConstants.BrokerFilesContainerName);
    }

    private async Task<TusStorageContext?> ResolveStorageContextAsync(string fileId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(fileId, out var fileTransferId))
        {
            return null;
        }

        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return null;
        }

        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            return null;
        }

        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return null;
        }

        var storageProvider = serviceOwner.GetStorageProvider(fileTransfer.UseVirusScan);
        if (storageProvider is null)
        {
            return null;
        }

        var connectionString = hostEnvironment.IsDevelopment()
            ? AzureConstants.AzuriteUrl
            : $"https://{storageProvider.ResourceName}.blob.core.windows.net";
        var authenticationMode = hostEnvironment.IsDevelopment()
            ? AzureBlobTusStoreAuthenticationMode.ConnectionString
            : AzureBlobTusStoreAuthenticationMode.SystemAssignedManagedIdentity;

        return new TusStorageContext(connectionString, authenticationMode, storageProvider.ResourceName);
    }

    private sealed record TusStorageContext(
        string ConnectionString,
        AzureBlobTusStoreAuthenticationMode AuthenticationMode,
        string StorageAccountName);
}
