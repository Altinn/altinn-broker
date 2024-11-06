using System.Security.Cryptography;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Services;

using Azure;
using Azure.Storage;
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
    private const int BLOCKS_BEFORE_COMMIT = 5;

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
            var content = await blobClient.DownloadStreamingAsync(new BlobDownloadOptions()
            {
                TransferValidation = new DownloadTransferValidationOptions()
                {
                    AutoValidateChecksum = false
                }
            }, cancellationToken);
            return content.Value.Content;
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

            using var accumulationBuffer = new MemoryStream();
            var networkReadBuffer = new byte[1024*1024]; 
            var blockList = new List<string>();
            long position = 0;
            using var blobMd5 = MD5.Create();

            int blocksInBatch = 0;

            while (position < length)
            {
                int bytesRead = await stream.ReadAsync(networkReadBuffer, 0, networkReadBuffer.Length, cancellationToken);
                if (bytesRead <= 0) break;

                accumulationBuffer.Write(networkReadBuffer, 0, bytesRead);
                position += bytesRead;

                bool isLastBlock = position >= length;
                if (accumulationBuffer.Length >= BLOCK_SIZE || isLastBlock)
                {
                    accumulationBuffer.Position = 0;
                    var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                    byte[] blockData = accumulationBuffer.ToArray();
                    using var blockStream = new MemoryStream(blockData, writable: false);
                    blobMd5.TransformBlock(blockData, 0, blockData.Length, null, 0);

                    await UploadBlock(blockBlobClient, blockId, blockData, cancellationToken);

                    blockList.Add(blockId);
                    blocksInBatch++;

                    // Clear accumulation buffer for next block
                    accumulationBuffer.SetLength(0);

                    // Commit intermediate blocks if we've accumulated enough
                    if (blocksInBatch >= BLOCKS_BEFORE_COMMIT)
                    {
                        logger.LogInformation($"Committing intermediate batch of {blocksInBatch} blocks at position " +
                            $"{position / (1024.0 * 1024.0 * 1024.0):N2} GiB");

                        await CommitBlocks(blockBlobClient, blockList, firstCommit: blockList.Count == blocksInBatch, null, cancellationToken);
                        blocksInBatch = 0;
                        // Keep the block list for the final commit
                        var uploadSpeedMBps = position / (1024.0 * 1024) / (stopwatch.ElapsedMilliseconds / 1000.0);
                        logger.LogInformation($"Upload progress for {fileTransferEntity.FileTransferId}: " +
                            $"{position / (1024.0 * 1024.0 * 1024.0):N2} GiB ({uploadSpeedMBps:N2} MB/s)");
                    }
                }
            }

            // Final commit with all blocks
            blobMd5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            if (blobMd5.Hash is null)
            {
                throw new Exception("Failed to calculate MD5 hash of uploaded file");
            }
            await CommitBlocks(blockBlobClient, blockList, firstCommit: blockList.Count <= BLOCKS_BEFORE_COMMIT, blobMd5.Hash, cancellationToken);
            blockList.Clear();

            double finalSpeedMBps = position / (1024.0 * 1024) / (stopwatch.ElapsedMilliseconds / 1000.0);
            logger.LogInformation($"Successfully uploaded {position / (1024.0 * 1024.0 * 1024.0):N2} GiB " +
                $"in {stopwatch.ElapsedMilliseconds / 1000.0:N1}s (avg: {finalSpeedMBps:N2} MB/s)");

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
        await RetryPolicy.ExecuteAsync(async () =>
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
        });
    }

    private async Task CommitBlocks(BlockBlobClient client, List<string> blockList, bool firstCommit, byte[]? finalMd5,
        CancellationToken cancellationToken)
    {
        await RetryPolicy.ExecuteAsync(async () =>
        {
            var options = new CommitBlockListOptions
            {
                // Only use ifNoneMatch for the first commit to ensure concurrent upload attempts do not work simultaneously
                Conditions = firstCommit ? new BlobRequestConditions { IfNoneMatch = new ETag("*") } : null,
                HttpHeaders = finalMd5 is null ? null : new BlobHttpHeaders
                {
                    ContentHash = finalMd5
                }
            };
            var response = await client.CommitBlockListAsync(blockList, options, cancellationToken);
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
