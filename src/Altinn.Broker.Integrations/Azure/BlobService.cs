using System.Buffers;
using System.Security.Cryptography;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Services;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Polly;

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
        try
        {
            BlobClient blobClient = await GetBlobClient(fileTransferEntity.FileTransferId, serviceOwnerEntity);
            BlockBlobClient blockBlobClient = new BlockBlobClient(blobClient.Uri);
            
            // Use smaller chunks to reduce memory pressure
            const int BufferSize = 1 * 1024 * 1024; // 1MB chunks
            using var buffer = new PooledBuffer(BufferSize); // Using object pool for buffers
            var blockList = new List<string>();
            long position = 0;
            
            while (position < length)
            {
                // Periodically yield to allow other operations
                if (position % (10 * BufferSize) == 0)
                {
                    await Task.Yield();
                }

                int bytesRead = await stream.ReadAsync(buffer.Array, 0, BufferSize, cancellationToken);
                if (bytesRead <= 0) break;

                var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                using var blockStream = new MemoryStream(buffer.Array, 0, bytesRead, writable: false);

                // Add exponential backoff retry for block uploads
                using var blockMd5 = MD5.Create();
                var blockHash = blockMd5.ComputeHash(buffer.Array, 0, bytesRead);
                await RetryPolicy.ExecuteAsync(async () =>
                {
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
                });

                blockList.Add(blockId);
                position += bytesRead;

                // Log progress less frequently
                if (position % (50 * BufferSize) == 0)
                {
                    var speedKBps = position / (stopwatch.ElapsedMilliseconds + 1);
                    logger.LogDebug($"Upload progress: {position}/{length} bytes ({speedKBps:N0} KB/s)");
                }

                // Commit blocks in smaller batches
                if (blockList.Count >= 10000 || position >= length)
                {
                    await CommitBlocks(blockBlobClient, blockList, cancellationToken);
                    blockList.Clear();
                }
            }

            logger.LogInformation($"Successfully uploaded {position:N0} bytes in {stopwatch.ElapsedMilliseconds:N0}ms");
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to upload file {fileTransferEntity.FileTransferId}");
            throw;
        }
    }

    private async Task CommitBlocks(BlockBlobClient client, List<string> blockList, CancellationToken cancellationToken)
    {
        await RetryPolicy.ExecuteAsync(async () =>
        {
            var response = await client.CommitBlockListAsync(
                blockList,
                new CommitBlockListOptions
                {
                    Conditions = new BlobRequestConditions { IfNoneMatch = new ETag("*") }
                },
                cancellationToken
            );
            
            logger.LogInformation($"Committed {blockList.Count} blocks: {response.GetRawResponse().ReasonPhrase}");
        });
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


// Helper class for buffer pooling
public class PooledBuffer : IDisposable
{
    private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;
    public byte[] Array { get; private set; }

    public PooledBuffer(int size)
    {
        Array = Pool.Rent(size);
    }

    public void Dispose()
    {
        if (Array != null)
        {
            Pool.Return(Array);
            Array = null!;
        }
    }
}

// Retry policy definition
public static class RetryPolicy
{
    private static readonly IAsyncPolicy RetryWithBackoff = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            3,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            (ex, timeSpan) => {
                // Log retry attempt
            }
        );

    public static Task ExecuteAsync(Func<Task> action) => RetryWithBackoff.ExecuteAsync(action);
}
