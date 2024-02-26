using Altinn.Broker.Repositories;

using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Azure;

public class BlobService : IFileStore
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

    public async Task<Stream> GetFileStream(Guid fileId, string connectionString, CancellationToken ct)
    {
        BlobClient blobClient = GetBlobClient(fileId, connectionString);
        try
        {
            var content = await blobClient.DownloadContentAsync(ct);
            return content.Value.Content.ToStream();
        }
        catch (RequestFailedException requestFailedException)
        {
            _logger.LogError("Error occurred while downloading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            throw;
        }
    }

    public async Task<string> UploadFile(Stream stream, Guid fileId, string connectionString, CancellationToken ct)
    {
        BlobClient blobClient = GetBlobClient(fileId, connectionString);
        var blobLeaseClient = blobClient.GetBlobLeaseClient();
        try
        {
            if (!await blobClient.ExistsAsync(ct))
            {
                await blobClient.UploadAsync(new MemoryStream());
            }
            BlobLease blobLease = await blobLeaseClient.AcquireAsync(TimeSpan.FromSeconds(-1), cancellationToken: ct);
            BlobUploadOptions options = new BlobUploadOptions()
            {
                Conditions = new BlobRequestConditions()
                {
                    LeaseId = blobLease.LeaseId
                },
                TransferValidation = new UploadTransferValidationOptions { ChecksumAlgorithm = StorageChecksumAlgorithm.MD5 },
            };
            var blobMetadata = await blobClient.UploadAsync(stream, options, ct);
            var metadata = blobMetadata.Value;
            var hash = Convert.ToHexString(metadata.ContentHash).ToLowerInvariant();
            return hash;
        }
        catch (RequestFailedException requestFailedException)
        {
            _logger.LogError("Error occurred while uploading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
            throw;
        }
        finally
        {
            await blobLeaseClient.BreakAsync();
        }
    }

    public async Task DeleteFile(Guid fileId, string connectionString, CancellationToken ct)
    {
        BlobClient blobClient = GetBlobClient(fileId, connectionString);
        try
        {
            await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
        }
        catch (RequestFailedException requestFailedException)
        {
            _logger.LogError("Error occurred while deleting file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            throw;
        }
    }
}
