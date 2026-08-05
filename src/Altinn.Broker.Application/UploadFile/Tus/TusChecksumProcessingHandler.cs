using Altinn.Broker.Application.Middlewares;
using Altinn.Broker.Application.UploadFile;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Application.UploadFile.Tus;

public class TusChecksumProcessingHandler(
    IFileTransferRepository fileTransferRepository,
    IFileTransferStatusRepository fileTransferStatusRepository,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IBrokerStorageService brokerStorageService,
    IIdempotencyEventRepository idempotencyEventRepository,
    FileTransferPublishService fileTransferPublishService,
    IBackgroundJobClient backgroundJobClient,
    EventBusMiddleware eventBus,
    IHostEnvironment hostEnvironment,
    IOptions<GeneralSettings> generalSettings,
    MalwareScanningResultHandler malwareScanResultHandler,
    ILogger<TusChecksumProcessingHandler> logger)
{
    private const string Md5ValidatedIdempotencySuffix = "_md5validated";
    private const string MalwareScanIdempotencySuffix = "_malwarescan";

    [AutomaticRetry(Attempts = 3)]
    public async Task Process(Guid fileTransferId, CancellationToken cancellationToken)
    {
        if (await idempotencyEventRepository.ExistsAsync($"{fileTransferId}{Md5ValidatedIdempotencySuffix}", cancellationToken))
        {
            logger.LogInformation("MD5 checksum already processed for {fileTransferId}", fileTransferId);
            return;
        }

        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            logger.LogError("File transfer {fileTransferId} not found during MD5 processing", fileTransferId);
            return;
        }

        if (fileTransfer.FileTransferStatusEntity.Status is FileTransferStatus.Failed or FileTransferStatus.Purged)
        {
            logger.LogInformation(
                "Skipping MD5 processing for {fileTransferId} because status is {status}",
                fileTransferId,
                fileTransfer.FileTransferStatusEntity.Status);
            return;
        }

        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            logger.LogError("Resource {resourceId} not found during MD5 processing for {fileTransferId}", fileTransfer.ResourceId, fileTransferId);
            return;
        }

        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            logger.LogError("Service owner not found during MD5 processing for {fileTransferId}", fileTransferId);
            return;
        }

        var computedChecksum = await brokerStorageService.ComputeFileChecksumAsync(serviceOwner, fileTransfer, cancellationToken);
        if (computedChecksum is null)
        {
            throw new InvalidOperationException($"Failed to compute MD5 checksum for file transfer {fileTransferId}");
        }

        var userProvidedChecksum = !string.IsNullOrWhiteSpace(fileTransfer.Checksum);
        if (userProvidedChecksum
            && !string.Equals(computedChecksum, fileTransfer.Checksum, StringComparison.InvariantCultureIgnoreCase))
        {
            logger.LogWarning(
                "Checksum mismatch for {fileTransferId}. Expected {expectedChecksum}, got {computedChecksum}",
                fileTransferId,
                fileTransfer.Checksum,
                computedChecksum);
            await TransactionWithRetriesPolicy.Execute(async ct =>
            {
                await fileTransferStatusRepository.InsertFileTransferStatus(
                    fileTransferId,
                    FileTransferStatus.Failed,
                    timestamp: DateTime.UtcNow,
                    detailedFileTransferStatus: "Checksum mismatch",
                    cancellationToken: ct);
                backgroundJobClient.Enqueue<IBrokerStorageService>(service =>
                    service.DeleteFile(serviceOwner, fileTransfer, ct));
                backgroundJobClient.Enqueue(() => eventBus.Publish(
                    AltinnEventType.UploadFailed,
                    fileTransfer.ResourceId,
                    fileTransferId.ToString(),
                    fileTransfer.Sender.ActorExternalId,
                    Guid.NewGuid(),
                    AltinnEventSubjectRole.Sender));
                return Task.CompletedTask;
            }, logger, cancellationToken);
            return;
        }

        await TransactionWithRetriesPolicy.Execute(async ct =>
        {
            if (!userProvidedChecksum)
            {
                await fileTransferRepository.SetChecksum(fileTransferId, computedChecksum, ct);
                fileTransfer.Checksum = computedChecksum;
            }

            await idempotencyEventRepository.TryAddIdempotencyEventAsync($"{fileTransferId}{Md5ValidatedIdempotencySuffix}", ct);
            backgroundJobClient.Enqueue<IBrokerStorageService>(service =>
                service.SetContentHashForExistingBlob(serviceOwner, fileTransfer, ct));
            return Task.CompletedTask;
        }, logger, cancellationToken);

        var storageProvider = serviceOwner.GetStorageProvider(fileTransfer.UseVirusScan);
        var requiresVirusScan = storageProvider?.Type == StorageProviderType.Altinn3Azure;
        var alreadyPublished = fileTransfer.FileTransferStatusEntity.Status == FileTransferStatus.Published;

        if (alreadyPublished)
        {
            return;
        }

        if (requiresVirusScan)
        {
            if (!await idempotencyEventRepository.ExistsAsync($"{fileTransferId}{MalwareScanIdempotencySuffix}", cancellationToken))
            {
                if (hostEnvironment.IsDevelopment()
                    && generalSettings.Value.SimulateMalwareScan
                    && userProvidedChecksum)
                {
                    await SimulateMalwareScanResult(fileTransferId);
                    return;
                }

                logger.LogInformation(
                    "MD5 validated for {fileTransferId}; awaiting malware scan before publishing",
                    fileTransferId);
                return;
            }

            if (fileTransfer.FileTransferStatusEntity.Status == FileTransferStatus.Failed)
            {
                return;
            }
        }

        await PublishFileTransferAsync(fileTransfer, cancellationToken);
    }

    private async Task PublishFileTransferAsync(FileTransferEntity fileTransfer, CancellationToken cancellationToken)
    {
        await TransactionWithRetriesPolicy.Execute(async ct =>
        {
            await fileTransferPublishService.TryPublishAsync(fileTransfer, DateTime.UtcNow, ct);
            return Task.CompletedTask;
        }, logger, cancellationToken);
    }

    private async Task SimulateMalwareScanResult(Guid fileTransferId)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            return;
        }

        logger.LogInformation("Simulating malware scan result for filetransfer {fileTransferId}", fileTransferId);

        var simulatedScanResult = new ScanResultData
        {
            BlobUri = $"http://127.0.0.1:10000/devstoreaccount1/brokerfiles/{fileTransferId}",
            CorrelationId = Guid.NewGuid(),
            ETag = "simulated-etag",
            ScanFinishedTimeUtc = DateTime.UtcNow,
            ScanResultDetails = new ScanResultDetails
            {
                MalwareNamesFound = new List<string>(),
                Sha256 = "simulated-sha256"
            },
            ScanResultType = "No threats found"
        };

        var result = await malwareScanResultHandler.Process(simulatedScanResult, null, CancellationToken.None);
        if (result.IsT1)
        {
            logger.LogError(
                "Error in simulated malware scan result for filetransfer {fileTransferId}: {Error}",
                fileTransferId,
                result.AsT1.Message);
        }
    }
}
