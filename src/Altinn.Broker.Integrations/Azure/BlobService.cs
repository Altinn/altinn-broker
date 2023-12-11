using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Altinn.Broker.Integrations.Azure;

public class BlobService : Repositories.IFileStore
{

    public BlobService()
    {
    }

    public async Task<Stream> GetFileStream(Guid fileId, string connectionString)
    {
        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient("brokerfiles");
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        var content = await blobClient.DownloadContentAsync();
        return content.Value.Content.ToStream();
    }

    public async Task UploadFile(Stream stream, Guid fileId, string connectionString)
    {
        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient("brokerfiles");
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        BlobUploadOptions options = new()
        {
            TransferValidation = new UploadTransferValidationOptions { ChecksumAlgorithm = StorageChecksumAlgorithm.MD5 }
        };
        await blobClient.UploadAsync(stream, options);
    }

    public async Task<bool> IsOnline(string connectionString)
    {
        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containers = blobServiceClient.GetBlobContainersAsync();
            await foreach (var container in containers)
            {
                // Accessing the containers. If this succeeds, the account exists.
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task DeleteFile(Guid fileId, string connectionString)
    {
        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient("brokerfiles");
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        await blobClient.DeleteIfExistsAsync();
    }
}
