using System.Security.Cryptography;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;

namespace Altinn.Broker.Integrations.Azure;

public class AzureStorageService(IResourceManager resourceManager, IOptions<AzureStorageOptions> azureStorageOptions, ILogger<AzureStorageService> logger) : IBrokerStorageService
{
    private async Task<BlobContainerClient> GetBlobContainerClient(FileTransferEntity fileTransferEntity, ServiceOwnerEntity serviceOwnerEntity)
    {
        var storageProvider = serviceOwnerEntity.GetStorageProvider(fileTransferEntity.UseVirusScan);
        var connectionString = await resourceManager.GetStorageConnectionString(storageProvider);
        var blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions()
        {
            Retry =
            {
                NetworkTimeout = TimeSpan.FromHours(24),
            }
        });
        var containerClient = blobServiceClient.GetBlobContainerClient("brokerfiles");
        return containerClient;
    }

    public async Task<Stream> DownloadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransfer, CancellationToken cancellationToken)
    {
        var blobContainerClient = await GetBlobContainerClient(fileTransfer, serviceOwnerEntity);
        var blobClient = blobContainerClient.GetBlobClient(fileTransfer.FileTransferId.ToString());
        try
        {
            var content = await blobClient.DownloadStreamingAsync(new BlobDownloadOptions(), cancellationToken);
            return content.Value.Content;
        }
        catch (RequestFailedException requestFailedException)
        {
            logger.LogError("Error occurred while downloading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            throw;
        }
    }

    public async Task<string?> UploadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity,
                                      Stream stream, long streamLength, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Starting upload of {fileTransferEntity.FileTransferId} for {serviceOwnerEntity.Name}");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var blobContainerClient = await GetBlobContainerClient(fileTransferEntity, serviceOwnerEntity);
        BlockBlobClient blockBlobClient = blobContainerClient.GetBlockBlobClient(fileTransferEntity.FileTransferId.ToString());
        try
        {

            using var accumulationBuffer = new MemoryStream();
            var networkReadBuffer = new byte[1024 * 1024];
            var blockList = new List<string>();
            long position = 0;
            using var blobMd5 = MD5.Create();

            int blocksInBatch = 0;
            var uploadTasks = new List<Task>();
            var semaphore = new SemaphoreSlim(azureStorageOptions.Value.ConcurrentUploadThreads); // Limit concurrent operations

            while (position < streamLength)
            {
                int bytesRead = await stream.ReadAsync(networkReadBuffer, 0, networkReadBuffer.Length, cancellationToken);
                if (bytesRead <= 0) break;

                accumulationBuffer.Write(networkReadBuffer, 0, bytesRead);
                position += bytesRead;

                bool isLastBlock = position >= streamLength;
                if (accumulationBuffer.Length >= azureStorageOptions.Value.BlockSize || isLastBlock)
                {
                    accumulationBuffer.Position = 0;
                    var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                    byte[] blockData = accumulationBuffer.ToArray();
                    blobMd5.TransformBlock(blockData, 0, blockData.Length, null, 0);

                    blockList.Add(blockId);
                    blocksInBatch++;
                    accumulationBuffer.SetLength(0); // Clear accumulation buffer for next block
                    await semaphore.WaitAsync(cancellationToken);
                    uploadTasks.Add(UploadBlockAsync(blockBlobClient, blockId, blockData, cancellationToken));
                    async Task UploadBlockAsync(BlockBlobClient client, string currentBlockId, byte[] currentBlockData, CancellationToken cancellationToken)
                    {
                        try
                        {
                            await UploadBlock(client, currentBlockId, currentBlockData, cancellationToken);

                            var uploadSpeedMBps = position / (1024.0 * 1024) / (stopwatch.ElapsedMilliseconds / 1000.0);
                            logger.LogInformation($"Uploaded block {blockList.Count}. Progress: " +
                                $"{position / (1024.0 * 1024.0 * 1024.0):N2} GiB ({uploadSpeedMBps:N2} MB/s)");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }

                    if (uploadTasks.Count >= azureStorageOptions.Value.BlocksBeforeCommit)
                    {
                        await Task.WhenAll(uploadTasks);

                        // Commit the blocks we have so far
                        var blocksToCommit = blockList.ToList();
                        var isFirstCommit = blockList.Count <= azureStorageOptions.Value.BlocksBeforeCommit;
                        await CommitBlocks(blockBlobClient, blocksToCommit, firstCommit: isFirstCommit, null, cancellationToken);

                        uploadTasks.Clear();
                    }
                }
            }
            await Task.WhenAll(uploadTasks);

            // Final commit with MD5 hash
            blobMd5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            if (blobMd5.Hash is null)
            {
                throw new Exception("Failed to calculate MD5 hash of uploaded file");
            }
            await CommitBlocks(blockBlobClient, blockList, firstCommit: blockList.Count <= azureStorageOptions.Value.BlocksBeforeCommit, blobMd5.Hash, cancellationToken);

            double finalSpeedMBps = position / (1024.0 * 1024) / (stopwatch.ElapsedMilliseconds / 1000.0);
            logger.LogInformation($"Successfully uploaded {position / (1024.0 * 1024.0 * 1024.0):N2} GiB " +
                $"in {stopwatch.ElapsedMilliseconds / 1000.0:N1}s (avg: {finalSpeedMBps:N2} MB/s)");

            return BitConverter.ToString(blobMd5.Hash).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            logger.LogError("Error occurred while uploading file: {errorMessage}: {stackTrace} ", ex.Message, ex.StackTrace);
            await blockBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            throw;
        }
    }

    private async Task UploadBlock(BlockBlobClient client, string blockId, byte[] blockData, CancellationToken cancellationToken)
    {
        await BlobRetryPolicy.ExecuteAsync(logger, async () =>
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
                throw new Exception($"Failed to upload block {blockId}: {blockResponse.GetRawResponse().Content}");
            }
        });
    }

    private async Task CommitBlocks(BlockBlobClient client, List<string> blockList, bool firstCommit, byte[]? finalMd5,
        CancellationToken cancellationToken)
    {
        await BlobRetryPolicy.ExecuteAsync(logger, async () =>
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
        var blobContainerClient = await GetBlobContainerClient(fileTransferEntity, serviceOwnerEntity);
        var blobClient = blobContainerClient.GetBlobClient(fileTransferEntity.FileTransferId.ToString());

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

internal static class BlobRetryPolicy
{
    private static IAsyncPolicy RetryWithBackoff(ILogger logger) => Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            3,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            (ex, timeSpan) => {
                logger.LogWarning($"Error during retries: {ex.Message}");
            }
        );

    public static Task ExecuteAsync(ILogger logger, Func<Task> action) => RetryWithBackoff(logger).ExecuteAsync(action);
}
