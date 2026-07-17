using System.Security.Claims;

using Altinn.Broker.Application.Middlewares;
using Altinn.Broker.Application.Settings;
using Altinn.Broker.Core;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OneOf;

namespace Altinn.Broker.Application.UploadFile;

public class CompleteFileUploadHandler(
    IFileTransferRepository fileTransferRepository,
    IFileTransferStatusRepository fileTransferStatusRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IResourceRepository resourceRepository,
    IBackgroundJobClient backgroundJobClient,
    EventBusMiddleware eventBus,
    IHostEnvironment hostEnvironment,
    IOptions<GeneralSettings> generalSettings,
    MalwareScanningResultHandler malwareScanResultHandler,
    ILogger<CompleteFileUploadHandler> logger) : IHandler<CompleteFileUploadRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(CompleteFileUploadRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var fileTransfer = await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }

        if (fileTransfer.FileTransferStatusEntity.Status is not (
            FileTransferStatus.UploadStarted or FileTransferStatus.UploadProcessing))
        {
            logger.LogInformation(
                "CompleteFileUpload skipped for file transfer {FileTransferId}: status already {CurrentStatus}",
                request.FileTransferId,
                fileTransfer.FileTransferStatusEntity.Status);
            return request.FileTransferId;
        }

        var alreadyUploadProcessing = fileTransfer.FileTransferStatusEntity.Status == FileTransferStatus.UploadProcessing;

        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.InvalidResourceDefinition;
        }

        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        }

        var storageProvider = serviceOwner.GetStorageProvider(fileTransfer.UseVirusScan);
        if (storageProvider is null)
        {
            return Errors.StorageProviderNotReady;
        }

        var finishedUploadTimestamp = DateTime.UtcNow;
        var userProvidedChecksum = !string.IsNullOrWhiteSpace(fileTransfer.Checksum);
        var requiresVirusScan = storageProvider.Type == StorageProviderType.Altinn3Azure;

        logger.LogInformation(
            "CompleteFileUpload processing file transfer {FileTransferId}. DeferChecksumValidation={DeferChecksumValidation} UserProvidedChecksum={UserProvidedChecksum} RequiresVirusScan={RequiresVirusScan}",
            request.FileTransferId,
            request.DeferChecksumValidation,
            userProvidedChecksum,
            requiresVirusScan);

        if (!request.DeferChecksumValidation
            && userProvidedChecksum
            && !string.Equals(request.Checksum, fileTransfer.Checksum, StringComparison.InvariantCultureIgnoreCase))
        {
            await fileTransferStatusRepository.InsertFileTransferStatus(
                request.FileTransferId,
                FileTransferStatus.Failed,
                timestamp: finishedUploadTimestamp,
                detailedFileTransferStatus: "Checksum mismatch",
                cancellationToken: cancellationToken);
            backgroundJobClient.Enqueue<IBrokerStorageService>(service =>
                service.DeleteFile(serviceOwner, fileTransfer, cancellationToken));
            return Errors.ChecksumMismatch;
        }

        try
        {
            await TransactionWithRetriesPolicy.Execute<Task>(async ct =>
            {
                if (request.DeferChecksumValidation)
                {
                    if (userProvidedChecksum || requiresVirusScan)
                    {
                        if (!alreadyUploadProcessing)
                        {
                            logger.LogInformation(
                                "CompleteFileUpload setting status UploadProcessing for file transfer {FileTransferId} (defer checksum, virus scan or user checksum)",
                                request.FileTransferId);
                            await fileTransferStatusRepository.InsertFileTransferStatus(
                                request.FileTransferId,
                                FileTransferStatus.UploadProcessing,
                                timestamp: finishedUploadTimestamp,
                                cancellationToken: ct);
                            backgroundJobClient.Enqueue(() => eventBus.Publish(
                                AltinnEventType.UploadProcessing,
                                fileTransfer.ResourceId,
                                request.FileTransferId.ToString(),
                                fileTransfer.Sender.ActorExternalId,
                                Guid.NewGuid(),
                                AltinnEventSubjectRole.Sender));
                        }
                    }
                    else
                    {
                        logger.LogInformation(
                            "CompleteFileUpload setting status Published for file transfer {FileTransferId} (defer checksum, no virus scan)",
                            request.FileTransferId);
                        await fileTransferStatusRepository.InsertFileTransferStatus(
                            request.FileTransferId,
                            FileTransferStatus.Published,
                            timestamp: finishedUploadTimestamp,
                            cancellationToken: ct);
                        backgroundJobClient.Enqueue(() => eventBus.Publish(
                            AltinnEventType.Published,
                            fileTransfer.ResourceId,
                            request.FileTransferId.ToString(),
                            fileTransfer.Sender.ActorExternalId,
                            Guid.NewGuid(),
                            AltinnEventSubjectRole.Sender));
                        foreach (var recipient in fileTransfer.RecipientCurrentStatuses)
                        {
                            backgroundJobClient.Enqueue(() => eventBus.Publish(
                                AltinnEventType.Published,
                                fileTransfer.ResourceId,
                                request.FileTransferId.ToString(),
                                recipient.Actor.ActorExternalId,
                                Guid.NewGuid(),
                                AltinnEventSubjectRole.Recipient));
                        }
                    }

                    await fileTransferRepository.SetStorageDetails(
                        request.FileTransferId,
                        storageProvider.Id,
                        request.FileTransferId.ToString(),
                        request.UploadLength,
                        ct);

                    backgroundJobClient.Enqueue<TusChecksumProcessingHandler>(handler =>
                        handler.Process(request.FileTransferId, CancellationToken.None));
                }
                else
                {
                    if (requiresVirusScan)
                    {
                        var currentFileTransfer = await fileTransferRepository.GetFileTransfer(request.FileTransferId, ct);
                        if (currentFileTransfer is null)
                        {
                            throw new InvalidOperationException(
                                $"File transfer {request.FileTransferId} not found when trying to set status to UploadProcessing");
                        } 
                        if (currentFileTransfer.FileTransferStatusEntity.Status == FileTransferStatus.UploadStarted)
                        {
                            logger.LogInformation(
                                "CompleteFileUpload setting status UploadProcessing for file transfer {FileTransferId} (requires virus scan)",
                                request.FileTransferId);
                            await fileTransferStatusRepository.InsertFileTransferStatus(
                                request.FileTransferId,
                                FileTransferStatus.UploadProcessing,
                                timestamp: finishedUploadTimestamp,
                                cancellationToken: ct);
                            backgroundJobClient.Enqueue(() => eventBus.Publish(
                                AltinnEventType.UploadProcessing,
                                fileTransfer.ResourceId,
                                request.FileTransferId.ToString(),
                                fileTransfer.Sender.ActorExternalId,
                                Guid.NewGuid(),
                                AltinnEventSubjectRole.Sender));
                        }
                    }
                    else if (!generalSettings.Value.SimulateMalwareScan)
                    {
                        await fileTransferStatusRepository.InsertFileTransferStatus(
                            request.FileTransferId,
                            FileTransferStatus.Published,
                            timestamp: finishedUploadTimestamp,
                            cancellationToken: ct);
                        backgroundJobClient.Enqueue(() => eventBus.Publish(
                            AltinnEventType.Published,
                            fileTransfer.ResourceId,
                            request.FileTransferId.ToString(),
                            fileTransfer.Sender.ActorExternalId,
                            Guid.NewGuid(),
                            AltinnEventSubjectRole.Sender));
                        foreach (var recipient in fileTransfer.RecipientCurrentStatuses)
                        {
                            backgroundJobClient.Enqueue(() => eventBus.Publish(
                                AltinnEventType.Published,
                                fileTransfer.ResourceId,
                                request.FileTransferId.ToString(),
                                recipient.Actor.ActorExternalId,
                                Guid.NewGuid(),
                                AltinnEventSubjectRole.Recipient));
                        }
                    }

                    await fileTransferRepository.SetStorageDetails(
                        request.FileTransferId,
                        storageProvider.Id,
                        request.FileTransferId.ToString(),
                        request.UploadLength,
                        ct);
                }

                return Task.CompletedTask;
            }, logger, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(
                "Unexpected error occurred while completing file upload: {errorMessage} \nStack trace: {stackTrace}",
                e.Message,
                e.StackTrace);
            return await TransactionWithRetriesPolicy.Execute(async ct =>
            {
                await fileTransferStatusRepository.InsertFileTransferStatus(
                    request.FileTransferId,
                    FileTransferStatus.Failed,
                    timestamp: DateTime.UtcNow,
                    detailedFileTransferStatus: "Error occurred while uploading fileTransfer",
                    cancellationToken: ct);
                backgroundJobClient.Enqueue(() => eventBus.Publish(
                    AltinnEventType.UploadFailed,
                    fileTransfer.ResourceId,
                    request.FileTransferId.ToString(),
                    fileTransfer.Sender.ActorExternalId,
                    Guid.NewGuid(),
                    AltinnEventSubjectRole.Sender));
                return Errors.UploadFailed;
            }, logger, cancellationToken);
        }

        var simulateMalwareScanNow = hostEnvironment.IsDevelopment()
            && generalSettings.Value.SimulateMalwareScan
            && (!request.DeferChecksumValidation || !userProvidedChecksum);

        if (simulateMalwareScanNow)
        {
            await SimulateMalwareScanResult(request.FileTransferId);
            backgroundJobClient.Enqueue(() => eventBus.Publish(
                AltinnEventType.Published,
                fileTransfer.ResourceId,
                request.FileTransferId.ToString(),
                fileTransfer.Sender.ActorExternalId,
                Guid.NewGuid(),
                AltinnEventSubjectRole.Sender));
            foreach (var recipient in fileTransfer.RecipientCurrentStatuses)
            {
                backgroundJobClient.Enqueue(() => eventBus.Publish(
                    AltinnEventType.Published,
                    fileTransfer.ResourceId,
                    request.FileTransferId.ToString(),
                    recipient.Actor.ActorExternalId,
                    Guid.NewGuid(),
                    AltinnEventSubjectRole.Recipient));
            }
        }

        if (!request.DeferChecksumValidation
            && !userProvidedChecksum
            && !string.IsNullOrWhiteSpace(request.Checksum)
            && (!requiresVirusScan || simulateMalwareScanNow))
        {
            await fileTransferRepository.SetChecksum(request.FileTransferId, request.Checksum!, cancellationToken);
        }

        return request.FileTransferId;
    }

    private async Task SimulateMalwareScanResult(Guid fileTransferId)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            logger.LogWarning("SimulateMalwareScanResult called outside development environment");
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
            var error = result.AsT1;
            logger.LogError(
                "Error in simulated malware scan result for filetransfer {fileTransferId}: {Error}",
                fileTransferId,
                error.Message);
        }
    }
}
