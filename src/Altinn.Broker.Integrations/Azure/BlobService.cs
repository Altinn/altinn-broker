using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Azure;

public class BlobService : Repositories.IFileStore
{

    private readonly ILogger<BlobService> _logger;
    public BlobService(ILogger<BlobService> logger)
    {
        _logger = logger;
    }

    private BlobClient GetBlobClient(Guid fileId, string connectionString)
    {
        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient("brokerfiles");
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        return blobClient;
    }

    public async Task<Stream> GetFileStream(Guid fileId, string connectionString)
    {
        BlobClient blobClient = GetBlobClient(fileId, connectionString);
        try
        {
            var content = await blobClient.DownloadContentAsync();
            return content.Value.Content.ToStream();
        }
        catch (RequestFailedException requestFailedException)
        {
            _logger.LogError("Error occurred while downloading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            throw;
        }
    }

    public async Task UploadFile(Stream stream, Guid fileId, string connectionString)
    {
        BlobClient blobClient = GetBlobClient(fileId, connectionString);
        var blobLeaseClient = blobClient.GetBlobLeaseClient();
        try
        {
            if (!await blobClient.ExistsAsync()) 
            { 
                await blobClient.UploadAsync(new MemoryStream());
            }
            BlobLease blobLease = await blobLeaseClient.AcquireAsync(TimeSpan.FromSeconds(-1));
            BlobUploadOptions options = new BlobUploadOptions()
            {
                Conditions = new BlobRequestConditions()
                {
                    LeaseId = blobLease.LeaseId
                },
                TransferValidation = new UploadTransferValidationOptions { ChecksumAlgorithm = StorageChecksumAlgorithm.MD5 },
            };
            await blobClient.UploadAsync(stream, options);
        }
        catch (RequestFailedException requestFailedException)
        {
            _logger.LogError("Error occurred while uploading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            await blobClient.DeleteIfExistsAsync();
            throw;
        } finally
        {
            await blobLeaseClient.BreakAsync();
        }
    }

    public async Task DeleteFile(Guid fileId, string connectionString)
    {
        BlobClient blobClient = GetBlobClient(fileId, connectionString);
        try
        {
            await blobClient.DeleteIfExistsAsync();
        }
        catch (RequestFailedException requestFailedException)
        {
            _logger.LogError("Error occurred while deleting file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            throw;
        }
    }
}
