using System.Security.Claims;

using Altinn.Broker.Application.Middlewares;
using Altinn.Broker.Application.Settings;
using Altinn.Broker.Core;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
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

public class UploadFileHandler(
    IAuthorizationService authorizationService,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IFileTransferRepository fileTransferRepository,
    IFileTransferStatusRepository fileTransferStatusRepository,
    IBrokerStorageService brokerStorageService,
    IBackgroundJobClient backgroundJobClient,
    EventBusMiddleware eventBus,
    IHostEnvironment hostEnvironment,
    IOptions<GeneralSettings> generalSettings,
    MalwareScanningResultHandler malwareScanResultHandler,
    ILogger<UploadFileHandler> logger) : IHandler<UploadFileRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(UploadFileRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Uploading file for file transfer {fileTransferId}", request.FileTransferId);
        var fileTransfer = await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await authorizationService.CheckAccessAsSender(user, fileTransfer.ResourceId, fileTransfer.Sender.ActorExternalId, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        if (request.IsLegacy && request.OnBehalfOfConsumer is not null && !fileTransfer.IsSender(request.OnBehalfOfConsumer))
        {
            return Errors.NoAccessToResource;
        }
        if (fileTransfer.FileTransferStatusEntity.Status > FileTransferStatus.UploadStarted)
        {
            return Errors.FileTransferAlreadyUploaded;
        }
        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.InvalidResourceDefinition;
        };
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        };
        var storageProvider = serviceOwner.GetStorageProvider(fileTransfer.UseVirusScan);
        if (storageProvider is null)
        {
            return Errors.StorageProviderNotReady;
        }
        if (fileTransfer.UseVirusScan && request.ContentLength > ApplicationConstants.MaxVirusScanUploadSize)
        {
            return Errors.FileSizeTooBig;
        }
        if (resource?.MaxFileTransferSize is not null && request.ContentLength > resource.MaxFileTransferSize)
        {
            return Errors.FileSizeTooBig;
        }

        await fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.UploadStarted, cancellationToken: cancellationToken);

        try
        {
            var checksum = await brokerStorageService.UploadFile(serviceOwner, fileTransfer, request.UploadStream, request.ContentLength, cancellationToken);
            if (checksum is null)
            {
                await fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.Failed, "File upload failed and was aborted", cancellationToken);
                return Errors.UploadFailed;
            }

            if (string.IsNullOrWhiteSpace(fileTransfer.Checksum))
            {
                await fileTransferRepository.SetChecksum(request.FileTransferId, checksum, cancellationToken);
            }
            else if (!string.Equals(checksum, fileTransfer.Checksum, StringComparison.InvariantCultureIgnoreCase))
            {
                await fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.Failed, "Checksum mismatch", cancellationToken);
                backgroundJobClient.Enqueue(() => brokerStorageService.DeleteFile(serviceOwner, fileTransfer, cancellationToken));
                return Errors.ChecksumMismatch;
            }
        }
        catch (Exception e)
        {
            logger.LogError("Unexpected error occurred while uploading file: {errorMessage} \nStack trace: {stackTrace}", e.Message, e.StackTrace);
            return await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
            {
                await fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.Failed, "Error occurred while uploading fileTransfer", cancellationToken);
                backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.UploadFailed, fileTransfer.ResourceId, request.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, Guid.NewGuid(), AltinnEventSubjectRole.Sender));
                return Errors.UploadFailed;
            }, logger, cancellationToken);
        }        
        
        await fileTransferRepository.SetStorageDetails(request.FileTransferId, storageProvider.Id, request.FileTransferId.ToString(), request.ContentLength, cancellationToken);
        
            await fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.UploadProcessing, cancellationToken: cancellationToken);
            backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.UploadProcessing, fileTransfer.ResourceId, request.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, Guid.NewGuid(), AltinnEventSubjectRole.Sender));
        

        if (hostEnvironment.IsDevelopment() && generalSettings.Value.SimulateMalwareScan)
        {
            await SimulateMalwareScanResult(request.FileTransferId);
            backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.Published, fileTransfer.ResourceId, request.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, Guid.NewGuid(), AltinnEventSubjectRole.Sender));
            foreach (var recipient in fileTransfer.RecipientCurrentStatuses)
            {
                backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.Published, fileTransfer.ResourceId, request.FileTransferId.ToString(), recipient.Actor.ActorExternalId, Guid.NewGuid(), AltinnEventSubjectRole.Recipient));
            }
        }
        return fileTransfer.FileTransferId;
    }



        /// <summary>
        /// Simulates a malware scan result for local development and tests by calling the MalwareScanResultHandler with fake ScanResultData.
        /// </summary>
        public async Task SimulateMalwareScanResult(Guid fileTransferId)
        {
            if (!hostEnvironment.IsDevelopment())
            {
                logger.LogWarning("SimulateMalwareScanResult called outside development environment");
                return;
            }

            logger.LogInformation("Simulating malware scan result for filetransfer {fileTransferId}", fileTransferId);

            var simulatedScanResult = new ScanResultData
            {
                BlobUri = $"http://127.0.0.1:10000/devstoreaccount1/filetransfer/{fileTransferId}",
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

            if (result.IsT0)
            {
                logger.LogInformation("Successfully simulated malware scan result for filetransfer {fileTransferId} using MalwareScanResultHandler", fileTransferId);
            }
            else
            {
                var error = result.AsT1;
                logger.LogError("Error in simulated malware scan result for filetransfer {fileTransferId}: {Error}", fileTransferId, error.Message);
            }
        }
}
