using System.Security.Claims;

using Altinn.Broker.Application.Settings;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.UploadFile;

public class UploadFileHandler(
    IAuthorizationService resourceRightsRepository,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IFileTransferRepository fileTransferRepository,
    IFileTransferStatusRepository fileTransferStatusRepository,
    IBrokerStorageService brokerStorageService,
    IBackgroundJobClient backgroundJobClient,
    IEventBus eventBus,
    IHostEnvironment hostEnvironment,
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
        var hasAccess = await resourceRightsRepository.CheckUserAccess(user, fileTransfer.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.FileTransferNotFound;
        };
        if (request.Token.Consumer != fileTransfer.Sender.ActorExternalId)
        {
            return Errors.FileTransferNotFound;
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
                await eventBus.Publish(AltinnEventType.UploadFailed, fileTransfer.ResourceId, request.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
                return Errors.UploadFailed;
            }, logger, cancellationToken);
        }
        return await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            await fileTransferRepository.SetStorageDetails(request.FileTransferId, storageProvider.Id, request.FileTransferId.ToString(), request.ContentLength, cancellationToken);
            if (storageProvider.Type == StorageProviderType.Altinn3Azure && !hostEnvironment.IsDevelopment())
            {
                await fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.UploadProcessing, cancellationToken: cancellationToken);
                await eventBus.Publish(AltinnEventType.UploadProcessing, fileTransfer.ResourceId, request.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
            }
            else
            {
                await fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.Published);
                await eventBus.Publish(AltinnEventType.Published, fileTransfer.ResourceId, request.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
                foreach (var recipient in fileTransfer.RecipientCurrentStatuses)
                {
                    await eventBus.Publish(AltinnEventType.Published, fileTransfer.ResourceId, request.FileTransferId.ToString(), recipient.Actor.ActorExternalId, cancellationToken);
                }
            }
            return fileTransfer.FileTransferId;
        }, logger, cancellationToken);
    }
}
