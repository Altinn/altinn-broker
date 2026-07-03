using System.Collections.Concurrent;
using System.Security.Cryptography;

using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Integrations.Azure;

using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

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
        CancellationToken cancellationToken);
    Task<byte[]> ComputeCommittedStagingMd5Async(string fileId, CancellationToken cancellationToken);
    Task SetCommittedStagingMd5Async(string fileId, byte[] md5Hash, CancellationToken cancellationToken);
    Task<bool> StagingBlobExistsAsync(string fileId, CancellationToken cancellationToken);
    Task<bool> HasStagedBlocksAsync(string fileId, CancellationToken cancellationToken);
    Task<long> GetCommittedStagingLengthAsync(string fileId, CancellationToken cancellationToken);
    Task<long> GetStagedBlocksLengthAsync(string fileId, CancellationToken cancellationToken);
    Task<long?> TryGetStagingUploadLengthAsync(string fileId, CancellationToken cancellationToken);
    Task InitializePartialStagingBlobAsync(string fileId, long uploadLength, CancellationToken cancellationToken);
    Task SetStagingUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken);
    Task<long> ConcatenatePartialStagingBlobsAsync(
        string finalFileId,
        IReadOnlyList<string> partialFileIds,
        CancellationToken cancellationToken);
    Task DeleteStagingBlobAsync(string fileId, CancellationToken cancellationToken);
    Task<bool> DestinationBlobExistsAsync(string fileId, CancellationToken cancellationToken);
    Task<long> GetDestinationBlobLengthAsync(string fileId, CancellationToken cancellationToken);
}

public class TusStorageResolver(
    IFileTransferRepository fileTransferRepository,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IHostEnvironment hostEnvironment,
    ITusExpirationDetailsStore expirationDetailsStore,
    ITusPartialUploadRegistry partialUploadRegistry,
    IHttpContextAccessor httpContextAccessor) : ITusStorageResolver
{
    private const int ConcatenationReadBufferSize = 4 * 1024 * 1024;

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
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/octet-stream"
                }
            },
            cancellationToken: cancellationToken);
    }

    public async Task<byte[]> ComputeCommittedStagingMd5Async(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        await using var contentStream = await blockBlobClient.OpenReadAsync(cancellationToken: cancellationToken);
        return await MD5.HashDataAsync(contentStream, cancellationToken);
    }

    public async Task SetCommittedStagingMd5Async(string fileId, byte[] md5Hash, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        var properties = await blockBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var metadata = properties.Value.Metadata.ToDictionary(static k => k.Key, static v => v.Value);
        metadata[AzureStorageConstants.TusMd5ChecksumMetadataKey] = Convert.ToBase64String(md5Hash);
        await blockBlobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
        await blockBlobClient.SetHttpHeadersAsync(
            new BlobHttpHeaders
            {
                ContentType = "application/octet-stream",
                ContentHash = md5Hash
            },
            cancellationToken: cancellationToken);
    }

    public async Task<bool> StagingBlobExistsAsync(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return false;
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        return await blockBlobClient.ExistsAsync(cancellationToken);
    }

    public async Task<bool> HasStagedBlocksAsync(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return false;
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        if (!await blockBlobClient.ExistsAsync(cancellationToken))
        {
            return false;
        }

        var blockList = await blockBlobClient.GetBlockListAsync(
            BlockListTypes.Uncommitted,
            cancellationToken: cancellationToken);
        return blockList.Value.UncommittedBlocks.Any();
    }

    public async Task<long> GetStagedBlocksLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return 0;
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        if (!await blockBlobClient.ExistsAsync(cancellationToken))
        {
            return 0;
        }

        var blockList = await blockBlobClient.GetBlockListAsync(
            BlockListTypes.Uncommitted,
            cancellationToken: cancellationToken);
        return blockList.Value.UncommittedBlocks.Sum(block => block.SizeLong);
    }

    public async Task<long?> TryGetStagingUploadLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return null;
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        if (!await blockBlobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var properties = await blockBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        if (properties.Value.Metadata.TryGetValue(AzureStorageConstants.TusUploadLengthMetadataKey, out var uploadLengthValue)
            && long.TryParse(uploadLengthValue, out var uploadLength))
        {
            return uploadLength;
        }

        return null;
    }

    public async Task InitializePartialStagingBlobAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        if (await blockBlobClient.ExistsAsync(cancellationToken))
        {
            await SetStagingUploadLengthAsync(fileId, uploadLength, cancellationToken);
            return;
        }

        await using var emptyStream = new MemoryStream();
        await blockBlobClient.UploadAsync(
            emptyStream,
            new BlobUploadOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    [AzureStorageConstants.TusUploadLengthMetadataKey] = uploadLength.ToString()
                },
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/octet-stream"
                }
            },
            cancellationToken);
    }

    public async Task SetStagingUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {fileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        if (!await blockBlobClient.ExistsAsync(cancellationToken))
        {
            return;
        }

        var properties = await blockBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var metadata = properties.Value.Metadata.ToDictionary(static k => k.Key, static v => v.Value);
        metadata[AzureStorageConstants.TusUploadLengthMetadataKey] = uploadLength.ToString();
        await blockBlobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
    }

    public async Task<long> GetCommittedStagingLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return 0;
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        if (!await blockBlobClient.ExistsAsync(cancellationToken))
        {
            return 0;
        }

        var properties = await blockBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        return properties.Value.ContentLength;
    }

    public async Task<long> ConcatenatePartialStagingBlobsAsync(
        string finalFileId,
        IReadOnlyList<string> partialFileIds,
        CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(finalFileId, cancellationToken);
        if (storageContext is null)
        {
            throw new InvalidOperationException($"Missing storage context for file id {finalFileId}");
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var finalBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, finalFileId));

        using var uploadMd5 = MD5.Create();
        var blockIds = new List<string>();
        long blockIndex = 0;
        long totalLength = 0;
        var readBuffer = new byte[ConcatenationReadBufferSize];

        foreach (var partialFileId in partialFileIds)
        {
            var partialBlobClient = containerClient.GetBlockBlobClient(
                Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, partialFileId));
            if (!await partialBlobClient.ExistsAsync(cancellationToken))
            {
                throw new InvalidOperationException($"Partial staging blob {partialFileId} does not exist.");
            }

            await using var partialStream = await partialBlobClient.OpenReadAsync(cancellationToken: cancellationToken);
            int bytesRead;
            while ((bytesRead = await partialStream.ReadAsync(readBuffer, cancellationToken)) > 0)
            {
                var blockId = BuildBlockId(blockIndex++);
                var blockData = readBuffer.AsSpan(0, bytesRead).ToArray();

                await using var chunkStream = new MemoryStream(blockData, writable: false);
                await finalBlobClient.StageBlockAsync(blockId, chunkStream, cancellationToken: cancellationToken);
                blockIds.Add(blockId);
                uploadMd5.TransformBlock(blockData, 0, blockData.Length, null, 0);
                totalLength += bytesRead;
            }
        }

        if (blockIds.Count == 0)
        {
            throw new InvalidOperationException($"Cannot concatenate partial uploads into file id {finalFileId} because no data was found.");
        }

        uploadMd5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var md5Hash = uploadMd5.Hash
            ?? throw new InvalidOperationException($"Failed to calculate MD5 for concatenated file id {finalFileId}.");

        await finalBlobClient.CommitBlockListAsync(
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

        foreach (var partialFileId in partialFileIds)
        {
            var partialBlobClient = containerClient.GetBlockBlobClient(
                Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, partialFileId));
            await partialBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }

        return totalLength;
    }

    public async Task DeleteStagingBlobAsync(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return;
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var blockBlobClient = containerClient.GetBlockBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileId));
        await blockBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> DestinationBlobExistsAsync(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return false;
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var destinationBlobClient = containerClient.GetBlobClient(fileId);
        return await destinationBlobClient.ExistsAsync(cancellationToken);
    }

    public async Task<long> GetDestinationBlobLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        var storageContext = await ResolveStorageContextAsync(fileId, cancellationToken);
        if (storageContext is null)
        {
            return 0;
        }

        var containerClient = GetBlobContainerClient(storageContext);
        var destinationBlobClient = containerClient.GetBlobClient(fileId);
        if (!await destinationBlobClient.ExistsAsync(cancellationToken))
        {
            return 0;
        }

        var properties = await destinationBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        return properties.Value.ContentLength;
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
        var fileTransferId = await ResolveFileTransferIdAsync(fileId, cancellationToken);
        if (fileTransferId is null)
        {
            return null;
        }

        return await ResolveStorageContextForFileTransferAsync(fileTransferId.Value, cancellationToken);
    }

    private async Task<Guid?> ResolveFileTransferIdAsync(string fileId, CancellationToken cancellationToken)
    {
        fileId = TusRouteHelper.NormalizePartialFileId(fileId);
        var httpContext = httpContextAccessor.HttpContext;
        var requestPath = httpContext?.Request.Path.Value;
        var isPartialRequest = TusRouteHelper.IsPartialUploadRequest(httpContext, fileId);

        if (isPartialRequest
            && TusRouteHelper.TryGetFileTransferIdFromPath(requestPath, out var pathFileTransferId)
            && await FileTransferExistsAsync(pathFileTransferId, cancellationToken))
        {
            return pathFileTransferId;
        }

        if (await partialUploadRegistry.TryGetFileTransferIdAsync(fileId, cancellationToken) is Guid mappedFileTransferId
            && await FileTransferExistsAsync(mappedFileTransferId, cancellationToken))
        {
            return mappedFileTransferId;
        }

        if (httpContext is not null
            && TusRouteHelper.TryGetFileTransferIdFromRoute(httpContext, out var routeFileTransferId)
            && await FileTransferExistsAsync(routeFileTransferId, cancellationToken))
        {
            return routeFileTransferId;
        }

        if (!isPartialRequest
            && Guid.TryParse(fileId, out var parsedFileTransferId)
            && await FileTransferExistsAsync(parsedFileTransferId, cancellationToken))
        {
            return parsedFileTransferId;
        }

        return null;
    }

    private async Task<bool> FileTransferExistsAsync(Guid fileTransferId, CancellationToken cancellationToken)
        => await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken) is not null;

    private async Task<TusStorageContext?> ResolveStorageContextForFileTransferAsync(
        Guid fileTransferId,
        CancellationToken cancellationToken)
    {
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

    private static string BuildBlockId(long blockIndex)
    {
        var blockId = blockIndex.ToString("D12");
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(blockId));
    }

    private sealed record TusStorageContext(
        string ConnectionString,
        AzureBlobTusStoreAuthenticationMode AuthenticationMode,
        string StorageAccountName);
}
