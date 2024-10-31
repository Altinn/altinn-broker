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
                NetworkTimeout = TimeSpan.FromHours(48),
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
        var length = httpContextAccessor.HttpContext.Request.ContentLength!;
        logger.LogInformation($"Starting upload of {fileTransferEntity.FileTransferId} for {serviceOwnerEntity.Name}");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using (var blobMd5 = MD5.Create())
        {
            try
            {
                BlobClient blobClient = await GetBlobClient(fileTransferEntity.FileTransferId, serviceOwnerEntity);
                BlockBlobClient blockBlobClient = new BlockBlobClient(blobClient.Uri);

                // Reuse the same buffer throughout the upload
                const int BufferSize = 4 * 1024 * 1024; // 4MB chunks for better memory management
                byte[] buffer = new byte[BufferSize];
                var blockList = new List<string>();
                long position = 0;

                while (position < length)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, BufferSize, cancellationToken);
                    if (bytesRead <= 0) break;

                    var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

                    // Use ArraySegment to avoid creating new MemoryStream instances
                    var segment = new ArraySegment<byte>(buffer, 0, bytesRead);
                    using var blockStream = new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false);
                    using var blockMd5 = MD5.Create();
                    var blockHash = blockMd5.ComputeHash(buffer, 0, bytesRead);
                    var blockResponse = await blockBlobClient.StageBlockAsync(
                        blockId,
                        blockStream,
                        blockHash,
                        conditions: null,
                        null,
                        cancellationToken: cancellationToken
                    );

                    if (blockResponse.GetRawResponse().Status != 201)
                    {
                        throw new Exception($"Failed to upload block {blockId}");
                    }

                    blockList.Add(blockId);
                    position += bytesRead;
                    blobMd5.TransformBlock(buffer, 0, bytesRead, null, 0);

                    if (position % (50 * BufferSize) == 0) // Log every 50 blocks
                    {
                        var speedKBps = position / (stopwatch.ElapsedMilliseconds + 1); // Avoid divide by zero
                        logger.LogInformation($"Upload progress: {position}/{length} bytes ({speedKBps:N0} KB/s)");
                    }

                    // Commit blocks every 50,000 blocks or at the end
                    if (blockList.Count >= 50000 || position >= length)
                    {
                        var response = await blockBlobClient.CommitBlockListAsync(
                            blockList,
                            new CommitBlockListOptions
                            {
                                Conditions = new BlobRequestConditions { IfNoneMatch = new ETag("*") }
                            },
                            cancellationToken
                        );

                        logger.LogInformation($"Committed {blockList.Count} blocks: {response.GetRawResponse().ReasonPhrase}");
                        blockList.Clear();
                        break;
                    }
                }

                logger.LogInformation($"Successfully uploaded {position:N0} bytes in {stopwatch.ElapsedMilliseconds:N0}ms");
                blobMd5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(blobMd5.Hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to upload file {fileTransferEntity.FileTransferId}");
                return null;
            }
            finally
            {
                stopwatch.Stop();
            }
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
