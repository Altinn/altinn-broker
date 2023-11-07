using Altinn.Broker.Persistence.Options;

using Azure.Storage.Blobs;

using Microsoft.Extensions.Options;

namespace Altinn.Broker.Persistence;

public class BlobStore : IFileStore
{
    private static string? _connectionString;

    public BlobStore(IOptions<StorageOptions> storageOptions)
    {
        _connectionString = storageOptions.Value.ConnectionString ?? throw new ArgumentNullException("StorageOptions__ConnectionString");
    }

    public async Task<Stream> GetFileStream(Guid fileId)
    {
        var containerClient = new BlobContainerClient(_connectionString, "files");
        await containerClient.CreateIfNotExistsAsync();
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        var content = await blobClient.DownloadContentAsync();
        return content.Value.Content.ToStream();
    }

    public async Task UploadFile(Stream stream, Guid fileId)
    {
        var containerClient = new BlobContainerClient(_connectionString, "files");
        await containerClient.CreateIfNotExistsAsync();
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        await blobClient.UploadAsync(stream, true);
    }
}