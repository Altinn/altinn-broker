using System.Security.Cryptography;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;

using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;

namespace Altinn.Broker.Integrations.Azure;

public class AzureStorageService(IOptions<AzureStorageOptions> azureStorageOptions, IOptions<ReportStorageOptions> reportStorageOptions, IHostEnvironment hostEnvironment, ILogger<AzureStorageService> logger) : IBrokerStorageService
{
    protected virtual async Task<BlobContainerClient> GetBlobContainerClient(FileTransferEntity fileTransferEntity, ServiceOwnerEntity serviceOwnerEntity)
    {
        if (hostEnvironment.IsDevelopment())
        {
            return new BlobServiceClient(AzureConstants.AzuriteUrl).GetBlobContainerClient("brokerfiles");
        }
        var storageProvider = serviceOwnerEntity.GetStorageProvider(fileTransferEntity.UseVirusScan);
        var connectionString = GetStorageConnectionString(storageProvider);
        var storageUri = new Uri(connectionString);
        var blobServiceClient = new BlobServiceClient(storageUri, new DefaultAzureCredential(), new BlobClientOptions()
        {
            Retry =
                {
                    NetworkTimeout = TimeSpan.FromHours(1),
                }
        });
        var containerClient = blobServiceClient.GetBlobContainerClient("brokerfiles");
        return containerClient;
    }

    private string GetStorageConnectionString(StorageProviderEntity? storageProviderEntity)
    {
        if (storageProviderEntity?.ResourceName == null)
        {
            throw new InvalidOperationException("Storage account has not been deployed");
        }
        return $"https://{storageProviderEntity.ResourceName}.blob.core.windows.net";
    }

    public async Task<BrokerFileDownload> DownloadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransfer, ByteRange? range, CancellationToken cancellationToken)
    {
        var blobContainerClient = await GetBlobContainerClient(fileTransfer, serviceOwnerEntity);
        var blobClient = blobContainerClient.GetBlobClient(fileTransfer.FileTransferId.ToString());
        try
        {
            var options = new BlobDownloadOptions();
            if (range is not null)
            {
                options.Range = new HttpRange(range.Value.Offset, range.Value.Length);
            }
            var content = await blobClient.DownloadStreamingAsync(options, cancellationToken);
            var details = content.Value.Details;
            return new BrokerFileDownload(
                Content: content.Value.Content,
                TotalLength: ParseTotalLengthFromContentRange(details.ContentRange) ?? (range is null ? details.ContentLength : fileTransfer.FileTransferSize),
                SegmentLength: details.ContentLength,
                ETag: details.ETag.ToString());
        }
        catch (RequestFailedException requestFailedException)
        {
            logger.LogError("Error occurred while downloading file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            throw;
        }
    }


    // Content-Range is only present on ranged responses and has the format "bytes {start}-{end}/{total}"
    private static long? ParseTotalLengthFromContentRange(string? contentRange)
    {
        if (string.IsNullOrEmpty(contentRange))
        {
            return null;
        }
        var separatorIndex = contentRange.LastIndexOf('/');
        if (separatorIndex >= 0 && long.TryParse(contentRange.AsSpan(separatorIndex + 1), out var totalLength))
        {
            return totalLength;
        }
        return null;
    }

    public async Task<(string? Checksum, long Length)?> UploadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity,

                                      Stream stream, CancellationToken cancellationToken)
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
            var createdBlobByThisAttempt = false;

            var uploadTasks = new List<Task>();
            using var semaphore = new SemaphoreSlim(azureStorageOptions.Value.ConcurrentUploadThreads);

            async Task FlushAccumulationBuffer()
            {
                if (accumulationBuffer.Length == 0) return;

                accumulationBuffer.Position = 0;
                var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                byte[] blockData = accumulationBuffer.ToArray();
                blobMd5.TransformBlock(blockData, 0, blockData.Length, null, 0);
                blockList.Add(blockId);
                accumulationBuffer.SetLength(0);

                await semaphore.WaitAsync(cancellationToken);
                uploadTasks.Add(UploadBlockAsync(blockBlobClient, blockId, blockData, cancellationToken));

                async Task UploadBlockAsync(BlockBlobClient client, string currentBlockId, byte[] currentBlockData, CancellationToken ct)
                {
                    try
                    {
                        await UploadBlock(client, currentBlockId, currentBlockData, ct);
                        var uploadSpeedMBps = position / (1024.0 * 1024) / (stopwatch.ElapsedMilliseconds / 1000.0);
                        logger.LogInformation($"Uploaded block {blockList.Count}. Progress: " +
                            $"{position / (1024.0 * 1024.0 * 1024.0):N2} GiB ({uploadSpeedMBps:N2} MB/s)");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }

                // Use interim commits to prevent too many uncommitted blocks for very large files
                // No interim commits with malware-scanned uploads as scan starts at first commit
                if (!fileTransferEntity.UseVirusScan && uploadTasks.Count >= azureStorageOptions.Value.BlocksBeforeCommit)
                {
                    await Task.WhenAll(uploadTasks);
                    var isFirstCommitForThisCall = !createdBlobByThisAttempt;
                    await CommitBlocks(blockBlobClient, blockList.ToList(), firstCommit: isFirstCommitForThisCall, null, cancellationToken);
                    if (isFirstCommitForThisCall)
                    {
                        createdBlobByThisAttempt = true;
                    }
                    uploadTasks.Clear();
                }
            }

            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(networkReadBuffer, 0, networkReadBuffer.Length, cancellationToken)) > 0)
            {
                accumulationBuffer.Write(networkReadBuffer, 0, bytesRead);
                position += bytesRead;

                if (accumulationBuffer.Length >= azureStorageOptions.Value.BlockSize)
                {
                    await FlushAccumulationBuffer();
                }
            }

            // Flush any remaining data in the buffer
            await FlushAccumulationBuffer();

            // Unconditional finalization � always await and commit remaining blocks
            if (uploadTasks.Count > 0)
                await Task.WhenAll(uploadTasks);

            blobMd5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            if (blobMd5.Hash is null)
                throw new Exception("Failed to calculate MD5 hash of uploaded file");

            var isFirstCommitForFinalCall = !createdBlobByThisAttempt;
            await CommitBlocks(blockBlobClient, blockList.ToList(), firstCommit: isFirstCommitForFinalCall, null, cancellationToken);

            double finalSpeedMBps = position / (1024.0 * 1024) / (stopwatch.ElapsedMilliseconds / 1000.0);
            logger.LogInformation($"Successfully uploaded {position / (1024.0 * 1024.0 * 1024.0):N2} GiB " +
                $"in {stopwatch.ElapsedMilliseconds / 1000.0:N1}s (avg: {finalSpeedMBps:N2} MB/s)");

            return (BitConverter.ToString(blobMd5.Hash).Replace("-", "").ToLowerInvariant(), position);
        }
        catch (Exception ex)
        {
            logger.LogError("Error occurred while uploading file: {errorMessage}: {stackTrace} ", ex.Message, ex.StackTrace);
            await blockBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            throw;
        }
    }
    protected virtual async Task UploadBlock(BlockBlobClient client, string blockId, byte[] blockData, CancellationToken cancellationToken)
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

    protected virtual async Task CommitBlocks(BlockBlobClient client, List<string> blockList, bool firstCommit, byte[]? finalMd5,
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
            var tusStagingAppendBlob = blobContainerClient.GetBlobClient(
                Path.Combine(AzureStorageConstants.TusStagingBlobPath, fileTransferEntity.FileTransferId.ToString()));
            await tusStagingAppendBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            var tusStagingBlockBlob = blobContainerClient.GetBlobClient(
                Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileTransferEntity.FileTransferId.ToString()));
            await tusStagingBlockBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException requestFailedException)
        {
            logger.LogError("Error occurred while deleting file: {errorCode}: {errorMessage} ", requestFailedException.ErrorCode, requestFailedException.Message);
            throw;
        }
    }

    public async Task<(string? Checksum, long Length)?> FinalizeTusUpload(
        ServiceOwnerEntity serviceOwnerEntity,
        FileTransferEntity fileTransferEntity,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Finalizing TUS upload for {fileTransferId}", fileTransferEntity.FileTransferId);
        var blobContainerClient = await GetBlobContainerClient(fileTransferEntity, serviceOwnerEntity);
        var blockStagingBlobClient = blobContainerClient.GetBlobClient(
            Path.Combine(AzureStorageConstants.TusBlockStagingBlobPath, fileTransferEntity.FileTransferId.ToString()));
        var appendStagingBlobClient = blobContainerClient.GetBlobClient(
            Path.Combine(AzureStorageConstants.TusStagingBlobPath, fileTransferEntity.FileTransferId.ToString()));
        var destinationBlobClient = blobContainerClient.GetBlockBlobClient(fileTransferEntity.FileTransferId.ToString());

        try
        {
            var stagingBlobClient = await ResolveTusStagingBlobClientAsync(
                blockStagingBlobClient,
                appendStagingBlobClient,
                cancellationToken);
            if (stagingBlobClient is null)
            {
                logger.LogError("TUS staging blob not found for {fileTransferId}", fileTransferEntity.FileTransferId);
                return null;
            }

            logger.LogInformation(
                "TUS staging blob resolved for {FileTransferId}. StagingPath={StagingPath}",
                fileTransferEntity.FileTransferId,
                stagingBlobClient.Name);

            var stagingProperties = await stagingBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var contentLength = stagingProperties.Value.ContentLength;
            string? checksum = null;
            if (stagingProperties.Value.Metadata.TryGetValue(AzureStorageConstants.TusMd5ChecksumMetadataKey, out var md5Base64)
                && !string.IsNullOrWhiteSpace(md5Base64))
            {
                checksum = BitConverter.ToString(Convert.FromBase64String(md5Base64)).Replace("-", "").ToLowerInvariant();
            }

            var copyOperation = await destinationBlobClient.StartCopyFromUriAsync(
                stagingBlobClient.Uri,
                new BlobCopyFromUriOptions
                {
                    Metadata = new Dictionary<string, string>()
                },
                cancellationToken);
            await copyOperation.WaitForCompletionAsync(cancellationToken);

            if (checksum is not null)
            {
                var contentHash = Convert.FromBase64String(md5Base64!);
                await destinationBlobClient.SetHttpHeadersAsync(
                    new BlobHttpHeaders
                    {
                        ContentType = "application/octet-stream",
                        ContentHash = contentHash
                    },
                    cancellationToken: cancellationToken);
            }
            else
            {
                await destinationBlobClient.SetHttpHeadersAsync(
                    new BlobHttpHeaders
                    {
                        ContentType = "application/octet-stream"
                    },
                    cancellationToken: cancellationToken);
            }

            await stagingBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            await appendStagingBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            await blockStagingBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            logger.LogInformation(
                "Finalized TUS upload for {fileTransferId}, size {contentLength}",
                fileTransferEntity.FileTransferId,
                contentLength);
            return (checksum, contentLength);
        }
        catch (Exception ex)
        {
            logger.LogError(
                "Error finalizing TUS upload for {fileTransferId}: {errorMessage}",
                fileTransferEntity.FileTransferId,
                ex.Message);
            await destinationBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            throw;
        }
    }

    private static async Task<BlobClient?> ResolveTusStagingBlobClientAsync(
        BlobClient blockStagingBlobClient,
        BlobClient appendStagingBlobClient,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in new[] { blockStagingBlobClient, appendStagingBlobClient })
        {
            try
            {
                await candidate.GetPropertiesAsync(cancellationToken: cancellationToken);
                return candidate;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
            }
        }

        return null;
    }

        public async Task<string> UploadReportFileToStorage(string fileName, Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = reportStorageOptions.Value.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ReportStorageOptions.ConnectionString is not configured");
            }
            var blobServiceClient = GetOrCreateBlobServiceClient(connectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient("reports");
            await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var blobClient = blobContainerClient.GetBlobClient(fileName);

            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

            logger.LogInformation("Successfully uploaded report file to blob storage: {fileName}", fileName);
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload report file to blob storage: {fileName}", fileName);
            throw;
        }
    }

    private BlobServiceClient GetOrCreateBlobServiceClient(string connectionString)
    {
        var connectionStringParts = connectionString.Split(';');
        if (connectionStringParts.Any(connectionStringPart => connectionStringPart.StartsWith("AccountName="))) // Using Broker's storage account
        {
            var storageResourceName = GetAccountNameFromConnectionString(connectionString) ?? throw new Exception("Failed to extract AccountName from connection string");
            var storageUri = new Uri($"https://{storageResourceName}.blob.core.windows.net");
            return new BlobServiceClient(storageUri, new DefaultAzureCredential());
        }
        var blobServiceClient = new BlobServiceClient(connectionString);
        return blobServiceClient;
    }

    public static string? GetAccountNameFromConnectionString(string connectionString)
    {
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("AccountName="))
            {
                return part.Substring("AccountName=".Length);
            }
        }
        return null;
    }

    public async Task SetContentHashForExistingBlob(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(fileTransferEntity.Checksum))
        {
            logger.LogError("Did not set checksum content hash because checksum was not found on file transfer");
            return;
        }
        var blobContainerClient = await GetBlobContainerClient(fileTransferEntity, serviceOwnerEntity);
        var blobClient = blobContainerClient.GetBlobClient(fileTransferEntity.FileTransferId.ToString());
        BlobHttpHeaders headers = new BlobHttpHeaders
        {
            ContentType = "application/octet-stream", // Set appropriate content type
            ContentHash = HexStringToByteArray(fileTransferEntity.Checksum)
        };
        await blobClient.SetHttpHeadersAsync(headers);
    }

    public async Task<string?> ComputeFileChecksumAsync(
        ServiceOwnerEntity serviceOwnerEntity,
        FileTransferEntity fileTransferEntity,
        CancellationToken cancellationToken)
    {
        var blobContainerClient = await GetBlobContainerClient(fileTransferEntity, serviceOwnerEntity);
        var blobClient = blobContainerClient.GetBlobClient(fileTransferEntity.FileTransferId.ToString());
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            logger.LogError("Cannot compute checksum because blob does not exist for {fileTransferId}", fileTransferEntity.FileTransferId);
            return null;
        }

        await using var contentStream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        var hash = await MD5.HashDataAsync(contentStream, cancellationToken);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static byte[] HexStringToByteArray(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            throw new ArgumentException("Hex string cannot be null or empty");

        hex = hex.Replace("-", "").Replace(" ", "");
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have an even length");

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < hex.Length; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }

        return bytes;
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
