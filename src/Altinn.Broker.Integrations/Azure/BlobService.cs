using Azure.Storage.Blobs;

using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Azure;

public class BlobService : Repositories.IFileStore
{

    public BlobService()
    {
    }

    public async Task<Stream> GetFileStream(Guid fileId, string connectionString)
    {
        var containerClient = new BlobContainerClient(new Uri(connectionString));
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        var content = await blobClient.DownloadContentAsync();
        return content.Value.Content.ToStream();
    }

    public async Task UploadFile(Stream stream, Guid fileId, string connectionString)
    {
        var containerClient = new BlobContainerClient(new Uri(connectionString));
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        await blobClient.UploadAsync(stream, true);
    }

    public async Task<bool> IsOnline(string connectionString)
    {
        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
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
