using Azure.Storage.Blobs;

namespace Altinn.Broker.Persistence;

public class BlobStore : IFileStore
{
    public BlobStore()
    {
        if (ConnectionString is null){
            throw new InvalidOperationException("No BlobStorageConnectionString was was configured in appsettings.");
        }
    }

    private static string? ConnectionString => Environment.GetEnvironmentVariable("BlobStorageConnectionString");
    
    public async Task UploadFile(Stream filestream, string shipmentId, string fileReference)
    {   
        var containerClient = new BlobContainerClient(ConnectionString, shipmentId);
        await containerClient.CreateIfNotExistsAsync();
        BlobClient blobClient = containerClient.GetBlobClient(fileReference);
        await blobClient.UploadAsync(filestream, true);
    }
}


