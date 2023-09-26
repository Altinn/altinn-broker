using Altinn.Broker.Persistence.Options;

using Azure.Storage.Blobs;

using Microsoft.Extensions.Options;

namespace Altinn.Broker.Persistence;

public class BlobStore : IFileStore
{
    private static string _connectionString;

    public BlobStore(IOptions<StorageOptions> storageOptions)
    {
        _connectionString = storageOptions.Value.ConnectionString ?? throw new ArgumentNullException("StorageOptions__ConnectionString");
    }
    
    public async Task UploadFile(Stream filestream, string shipmentId, string fileReference)
    {   
        var containerClient = new BlobContainerClient(_connectionString, shipmentId);
        await containerClient.CreateIfNotExistsAsync();
        BlobClient blobClient = containerClient.GetBlobClient(fileReference);
        await blobClient.UploadAsync(filestream, true);
    }
}
