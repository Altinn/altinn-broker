using Altinn.Broker.Persistence;
using Altinn.Broker.Persistence.Options;

using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

public class BlobStoreTests
{
    [Fact]
    public async void StoreBlob_HappyPath_BlobIsStored()
    {
        // Arrange
        IOptions<StorageOptions> options = Options.Create(new StorageOptions(){
            ConnectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"
        });
        var blobStore = new BlobStore(options);

        // Act
        FileStream fileStream = File.Open("../../../Persistence/data/test.txt", FileMode.Open);
        string shipmentId = "testshipmentid";
        string fileReference = "test.txt";
        await blobStore.UploadFile(fileStream, shipmentId, fileReference);

        // Assert
        var containerClient = new BlobContainerClient(Environment.GetEnvironmentVariable("BlobStorageConnectionString") ?? "", shipmentId);
        await containerClient.CreateIfNotExistsAsync(publicAccessType: Azure.Storage.Blobs.Models.PublicAccessType.BlobContainer);
        BlobClient blobClient = containerClient.GetBlobClient(fileReference);
        Assert.True(blobClient.Exists());
    }
}

