using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Altinn.Broker.Persistence;

public class BlobStore : IFileStore
{
    public BlobStore()
    {
    }

    private static string? ConnectionString => Environment.GetEnvironmentVariable("BlobStorageConnectionString");
    
    public async Task UploadFile(Stream filestream, string shipmentId, string fileReference)
    {   
        if (ConnectionString is null){
            throw new Exception("No BlobStorageConnectionString was was configured in appsettings.");
        }
        var containerClient = new BlobContainerClient(ConnectionString, shipmentId);
        await containerClient.CreateIfNotExistsAsync();
        BlobClient blobClient = containerClient.GetBlobClient(fileReference);
        await blobClient.UploadAsync(filestream, true);
    }
}


