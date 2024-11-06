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
    private const int BLOCK_SIZE = 1024 * 1024 * 32; // 32MB
    private const int BLOCKS_BEFORE_COMMIT = 1000;
    private const int UPLOAD_THREADS = 3; // Test how much we can read from the stream first

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

    public async Task<string?> UploadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity,
    Stream stream, CancellationToken cancellationToken)
    {
        var length = httpContextAccessor.HttpContext.Request.ContentLength!;
        logger.LogInformation($"Starting upload of {fileTransferEntity.FileTransferId} for {serviceOwnerEntity.Name}");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            BlobClient blobClient = await GetBlobClient(fileTransferEntity.FileTransferId, serviceOwnerEntity);
            BlockBlobClient blockBlobClient = new BlockBlobClient(blobClient.Uri);

            int desiredBlockSize = 1024 * 1024 * 32; // 32MB
            using var accumulationBuffer = new MemoryStream();
            using var buffer = new PooledBuffer(1024 * 64); // 1MB read buffer, todo set as same as network buffer size
            var blockList = new List<string>();
            long position = 0;
            using var blobMd5 = MD5.Create();

            const int BLOCKS_BEFORE_COMMIT = 1000;
            int blocksInBatch = 0;

            while (position < length)
            {
                int bytesRead = await stream.ReadAsync(buffer.Array, 0, buffer.Array.Length, cancellationToken);
                if (bytesRead <= 0) break;

                /*accumulationBuffer.Write(buffer.Array, 0, bytesRead);
                position += bytesRead;

                bool isLastBlock = position >= length;
                if (accumulationBuffer.Length >= desiredBlockSize || isLastBlock)
                {
                    accumulationBuffer.Position = 0;
                    var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

                    byte[] blockData = accumulationBuffer.ToArray();
                    using var blockStream = new MemoryStream(blockData, writable: false);

                    blobMd5.TransformBlock(blockData, 0, blockData.Length, null, 0);

                    //await RetryPolicy.ExecuteAsync(async () =>
                    //{
                        using var blockMd5 = MD5.Create();
                        blockStream.Position = 0;
                        var blockResponse = await blockBlobClient.StageBlockAsync(
                            blockId,
                            blockStream,
                            blockMd5.ComputeHash(blockData),
                            conditions: null,
                            null,
                            cancellationToken: cancellationToken
                        );

                        if (blockResponse.GetRawResponse().Status != 201)
                        {
                            throw new Exception($"Failed to upload block {blockId}");
                        }

                        double uploadSpeedMBps = position / (1024.0 * 1024) / (stopwatch.ElapsedMilliseconds / 1000.0);
                        logger.LogDebug($"Upload progress for {fileTransferEntity.FileTransferId}: " +
                            $"{position}/{length} bytes ({uploadSpeedMBps:N2} MB/s)");
                    //});

                    blockList.Add(blockId);
                    blocksInBatch++;

                    // Clear accumulation buffer for next block
                    accumulationBuffer.SetLength(0);

                    // Commit intermediate blocks if we've accumulated enough
                    if (blocksInBatch >= BLOCKS_BEFORE_COMMIT)
                    {
                        logger.LogInformation($"Committing intermediate batch of {blocksInBatch} blocks at position " +
                            $"{position / (1024.0 * 1024.0 * 1024.0):N2} GiB");

                        await CommitBlocks(blockBlobClient, blockList, firstCommit: blockList.Count == blocksInBatch, cancellationToken);
                        blocksInBatch = 0;
                        // Keep the block list for the final commit
                        uploadSpeedMBps = position / (1024.0 * 1024) / (stopwatch.ElapsedMilliseconds / 1000.0);
                        logger.LogInformation($"Upload progress for {fileTransferEntity.FileTransferId}: " +
                            $"{position / (1024.0 * 1024.0 * 1024.0):N2} GiB ({uploadSpeedMBps:N2} MB/s)");
                    }
                }*/
            }

            // Final commit with all blocks
            /*await CommitBlocks(blockBlobClient, blockList, firstCommit: blockList.Count <= BLOCKS_BEFORE_COMMIT, cancellationToken);
            blockList.Clear();*/

            double finalSpeedMBps = position / (1024.0 * 1024) / (stopwatch.ElapsedMilliseconds / 1000.0);
            logger.LogInformation($"Successfully uploaded {position / (1024.0 * 1024.0 * 1024.0):N2} GiB " +
                $"in {stopwatch.ElapsedMilliseconds / 1000.0:N1}s (avg: {finalSpeedMBps:N2} MB/s)");

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

    private async Task UploadBlock(BlockBlobClient client, string blockId, byte[] blockData, CancellationToken cancellationToken)
    {
        using var blockMd5 = MD5.Create();
        using var blockStream = new MemoryStream(blockData, writable: false);
        blockStream.Position = 0;
        var blockResponse = await client.StageBlockAsync(
            blockId,
            blockStream,
            blockMd5.ComputeHash(blockData),
            conditions: null,
            null,
            cancellationToken: cancellationToken
        );

        if (blockResponse.GetRawResponse().Status != 201)
        {
            throw new Exception($"Failed to upload block {blockId}");
        }
    }

    private async Task CommitBlocks(BlockBlobClient client, List<string> blockList, bool firstCommit,
        CancellationToken cancellationToken)
    {
        //await RetryPolicy.ExecuteAsync(async () =>
        //{
            var options = new CommitBlockListOptions
            {
                // Only use ifNoneMatch for the first commit to ensure concurrent upload attempts do not work simultaneously
                Conditions = firstCommit ? new BlobRequestConditions { IfNoneMatch = new ETag("*") } : null
            };

            var response = await client.CommitBlockListAsync(blockList, options, cancellationToken);
            logger.LogInformation($"Committed {blockList.Count} blocks: {response.GetRawResponse().ReasonPhrase}");
        //});
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
