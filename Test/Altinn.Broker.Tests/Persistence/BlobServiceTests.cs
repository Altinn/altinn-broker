using Altinn.Broker.Persistence.Options;
using Altinn.Broker.Persistence.Storage;

using Azure.Storage.Blobs;

using Microsoft.Extensions.Options;

public class BlobServiceTests
{
    [Fact]
    public async void StoreBlob_HappyPath_BlobIsStored()
    {
        // Arrange
        IOptions<StorageOptions> options = Options.Create(new StorageOptions()
        {
            ConnectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"
        });
        var blobStore = new BlobService(options);

        // Act
        FileStream fileStream = File.Open("../../../Persistence/data/test.txt", FileMode.Open);
        var fileId = Guid.NewGuid();
        string fileReference = "test.txt";
        await blobStore.UploadFile(fileStream, fileId, null);

        // Assert
        var containerClient = new BlobContainerClient(options.Value.ConnectionString, "files");
        await containerClient.CreateIfNotExistsAsync(publicAccessType: Azure.Storage.Blobs.Models.PublicAccessType.BlobContainer);
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        Assert.True(blobClient.Exists());
    }
}
