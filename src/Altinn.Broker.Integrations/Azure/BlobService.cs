using System.Security.Cryptography;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Services;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Azure;

public class BlobService(IResourceManager resourceManager, IHttpContextAccessor httpContextAccessor, ILogger<BlobService> logger) : IBrokerStorageService
{
    private async Task<BlobClient> GetBlobClient(Guid fileId, ServiceOwnerEntity serviceOwnerEntity)
    {
        var connectionString = await resourceManager.GetStorageConnectionString(serviceOwnerEntity);
        var blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions()
        {
            Retry =
            {
                NetworkTimeout = TimeSpan.FromHours(48)
            }
        });
        var containerClient = blobServiceClient.GetBlobContainerClient("brokerfiles");
        BlobClient blobClient = containerClient.GetBlobClient(fileId.ToString());
        return blobClient;
    }

    public async Task<Stream> DownloadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransfer, CancellationToken cancellationToken)
    {
        BlobClient blobClient = await GetBlobClient(fileTransfer.FileTransferId, serviceOwnerEntity);
        try
        {
            var content = await blobClient.DownloadContentAsync(cancellationToken);
            return content.Value.Content.ToStream();
        }
        catch (RequestFailedException requestFailedException)
        {
            logger.LogError("Error occurred while downloading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            throw;
        }
    }

    public async Task<string?> UploadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity, Stream stream, CancellationToken cancellationToken)
    {
        var length = httpContextAccessor.HttpContext.Request.ContentLength;
        logger.LogInformation($"Starting upload of {fileTransferEntity.FileTransferId} for {serviceOwnerEntity.Name}");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        BlobClient blobClient = await GetBlobClient(fileTransferEntity.FileTransferId, serviceOwnerEntity);
        var fullBlobUrl = $"{blobClient.Uri}";
        BlockBlobClient blockBlobClient = new BlockBlobClient(new Uri(fullBlobUrl));
        using (var md5 = MD5.Create()) 
        {
            try
            {
                long position = 0;
                while (position < length) 
                {
                    // Read and upload chunks
                    int bufferSize = 32 * 1024 * 1024; // 32 MB chunks
                    if ((length - position) < bufferSize)
                    {
                        bufferSize = (int)(stream.Length - position);
                    }
                    var buffer = new byte[bufferSize];
                    int bytesRead;
                    var blockList = new List<string>();

                    while (blockList.Count < 50000 && (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                        using var ms = new MemoryStream(buffer, 0, bytesRead); // Will be continously disposed of every iteration such that our memory use stays equal to buffer size
                        var blockResponse = await blockBlobClient.StageBlockAsync(blockId, ms);
                        if (blockResponse.GetRawResponse().Status != 201)
                        {
                            throw new Exception($"Failed to upload block {blockId}");
                        }
                        blockList.Add(blockId);
                        position += bytesRead;
                        md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                        logger.LogDebug($"Current speed of file transfer {fileTransferEntity.FileTransferId} is {(position / (stopwatch.ElapsedMilliseconds)).ToString("N0")} KB/s");
                    }

                    // Commit the upload
                    var response = await blockBlobClient.CommitBlockListAsync(blockList, new CommitBlockListOptions()
                    {
                        Conditions = new BlobRequestConditions
                        {
                            IfNoneMatch = new ETag("*")
                        },

                    });
                    logger.LogInformation($"Committed blocks: {response.GetRawResponse().ReasonPhrase}");
                }
                logger.LogInformation($"Successfully committed {position.ToString("N0")} bytes");
                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                stopwatch.Stop();
                Console.WriteLine($"Upload for {fileTransferEntity.FileTransferId} completed in {stopwatch.ElapsedMilliseconds.ToString("N0")} ms");
            }
            return null;
        }

    }

    public async Task DeleteFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity, CancellationToken cancellationToken)
    {
        BlobClient blobClient = await GetBlobClient(fileTransferEntity.FileTransferId, serviceOwnerEntity);
        try
        {
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException requestFailedException)
        {
            logger.LogError("Error occurred while deleting file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            throw;
        }
    }
}
