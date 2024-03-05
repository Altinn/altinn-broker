using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Services;

using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Azure;

public class BlobService : IBrokerStorageService
{
    private readonly IResourceManager _resourceManager;
    private readonly ILogger<BlobService> _logger;
    public BlobService(IResourceManager resourceManager, ILogger<BlobService> logger)
    {
        _resourceManager = resourceManager;
        _logger = logger;
    }

    private async Task<BlobClient> GetBlobClient(Guid fileId, ServiceOwnerEntity serviceOwnerEntity)
    {
        var connectionString = await _resourceManager.GetStorageConnectionString(serviceOwnerEntity);
        var blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions()
        {
            Retry =
            {
                NetworkTimeout = TimeSpan.FromHours(1),
            }
        });
        var containerClient = blobServiceClient.GetBlobContainerClient("brokerfiles");
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        return blobClient;
    }

    public async Task<Stream> DownloadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransfer, CancellationToken cancellationToken)
    {
        BlobClient blobClient = await GetBlobClient(fileTransfer.FileTransferId, serviceOwnerEntity);
        try
        {
            var content = await blobClient.DownloadContentAsync(cancellationToken);
            return content.Value.Content.ToStream();
        }
        catch (RequestFailedException requestFailedException)
        {
            _logger.LogError("Error occurred while downloading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            throw;
        }
    }

    public async Task<string> UploadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity, Stream stream, CancellationToken cancellationToken)
    {
        BlobClient blobClient = await GetBlobClient(fileTransferEntity.FileTransferId, serviceOwnerEntity);
        try
        {
            BlobUploadOptions options = new BlobUploadOptions()
            {
                TransferValidation = new UploadTransferValidationOptions { ChecksumAlgorithm = StorageChecksumAlgorithm.MD5 }
            };
            var blobMetadata = await blobClient.UploadAsync(stream, options, cancellationToken);
            var metadata = blobMetadata.Value;
            var hash = Convert.ToHexString(metadata.ContentHash).ToLowerInvariant();
            return hash;
        }
        catch (RequestFailedException requestFailedException)
        {
            _logger.LogError("Error occurred while uploading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            throw;
        }
    }

    public async Task DeleteFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity, CancellationToken cancellationToken)
    {
        BlobClient blobClient = await GetBlobClient(fileTransferEntity.FileTransferId, serviceOwnerEntity);
        try
        {
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException requestFailedException)
        {
            _logger.LogError("Error occurred while deleting file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            throw;
        }
    }
}
