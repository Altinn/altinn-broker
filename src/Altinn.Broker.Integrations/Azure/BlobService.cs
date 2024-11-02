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
        var length = httpContextAccessor.HttpContext.Request.ContentLength!; // Temporary fix for getting content length
        logger.LogInformation($"Starting upload of {fileTransferEntity.FileTransferId} for {serviceOwnerEntity.Name}");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();        
        try
        {
            BlobClient blobClient = await GetBlobClient(fileTransferEntity.FileTransferId, serviceOwnerEntity);
            BlockBlobClient blockBlobClient = new BlockBlobClient(blobClient.Uri);
            
            int BufferSize = CaclulateBlockSize(length.Value);
            using var buffer = new PooledBuffer(BufferSize);
            var blockList = new List<string>();
            long position = 0;
            using var blobMd5 = MD5.Create();

            while (position < length)
            {
                int bytesRead = await stream.ReadAsync(buffer.Array, 0, BufferSize, cancellationToken);
                if (bytesRead <= 0) break;
                
                var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                using var blockStream = new MemoryStream(buffer.Array, 0, bytesRead, writable: false);

                blobMd5.TransformBlock(buffer.Array, 0, bytesRead, null, 0);
                await RetryPolicy.ExecuteAsync(async () =>
                {
                    using var blockMd5 = MD5.Create();
                    blockStream.Position = 0;
                    var blockResponse = await blockBlobClient.StageBlockAsync(
                        blockId,
                        blockStream,
                        blockMd5.ComputeHash(buffer.Array, 0, bytesRead),
                        conditions: null,
                        null,
                        cancellationToken: cancellationToken
                    );

                    if (blockResponse.GetRawResponse().Status != 201)
                    {
                        throw new Exception($"Failed to upload block {blockId}");
                    }
                    logger.LogDebug($"Upload progress for {fileTransferEntity.FileTransferId}: {position}/{length} bytes ({position / (stopwatch.ElapsedMilliseconds + 1):N0} KB/s)");
                });

                blockList.Add(blockId);
                position += bytesRead;

                if (position % (10 * BufferSize) == 0)
                {
                    logger.LogInformation($"Upload progress for {fileTransferEntity.FileTransferId}: {position}/{length} bytes ({position / (stopwatch.ElapsedMilliseconds + 1):N0} KB/s)");
                }
            }
            await CommitBlocks(blockBlobClient, blockList, cancellationToken);
            blockList.Clear();

            logger.LogInformation($"Successfully uploaded {position:N0} bytes in {stopwatch.ElapsedMilliseconds:N0}ms");
            blobMd5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            if (blobMd5.Hash is null)
            {
                throw new Exception("Failed to calculate MD5 hash of uploaded file");
            }
            return BitConverter.ToString(blobMd5.Hash).Replace("-", "").ToLowerInvariant();
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

    private int CaclulateBlockSize(long contentLength)
    {
        int defaultSize = (1024 * 1024) * 32; // 32 Mebibytes
        int maxBlocks = 50000; // Max number of blocks in a block blob
        int maxBlockSize = (1024 * 1024) * 2000; //2k Mebibytes
        if (((long)defaultSize * maxBlocks) > contentLength) // ~1.6TB
        {
            return defaultSize;
        }
        var requiredBlockSize = contentLength / maxBlocks;
        if (requiredBlockSize < maxBlockSize)
        {
            return maxBlockSize;
        }
        else
        {
            throw new ArgumentException($"File size is too large to upload. The limit is {(maxBlockSize * maxBlocks) / (1024^4)} TiB"); // ~100 TB
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


// Helper class for buffer pooling
public class PooledBuffer : IDisposable
{
    private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared; // Because static, shared between all instances
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
