using Altinn.Broker.Persistence.Options;

using Azure.Storage.Blobs;

using Microsoft.Azure.Management.Storage;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Persistence;

public class BlobStore : IFileStore
{
    private static string _connectionString;

    public BlobStore(IOptions<StorageOptions> storageOptions)
    {
        _connectionString = storageOptions.Value.ConnectionString ?? throw new ArgumentNullException("StorageOptions__ConnectionString");
    }

    public async Task<Stream> GetFileStream(Guid fileId, string? connectionString)
    {
        var containerClient = new BlobContainerClient(connectionString ?? _connectionString, "files");
        await containerClient.CreateIfNotExistsAsync();
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        var content = await blobClient.DownloadContentAsync();
        return content.Value.Content.ToStream();
    }

    public async Task UploadFile(Stream stream, Guid fileId, string? connectionString)
    {
        var containerClient = new BlobContainerClient(connectionString ?? _connectionString, "files");
        await containerClient.CreateIfNotExistsAsync();
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        await blobClient.UploadAsync(stream, true);
    }

    public async Task<bool> IsOnline(string? connectionString) 
    {
        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString ?? _connectionString);
            var containers = blobServiceClient.GetBlobContainers();
            foreach (var container in containers)
            {
                // Accessing the containers. If this succeeds, the account exists.
            }

            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
}
