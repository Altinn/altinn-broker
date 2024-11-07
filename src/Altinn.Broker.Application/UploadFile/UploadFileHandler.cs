using Altinn.Broker.Application.Settings;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OneOf;

namespace Altinn.Broker.Application.UploadFile;

public class UploadFileHandler(IAuthorizationService resourceRightsRepository, IResourceRepository resourceRepository, IServiceOwnerRepository serviceOwnerRepository, IFileTransferRepository fileTransferRepository, IFileTransferStatusRepository fileTransferStatusRepository, IBrokerStorageService brokerStorageService, IBackgroundJobClient backgroundJobClient, IEventBus eventBus, ILogger<UploadFileHandler> logger, IOptions<ApplicationSettings> applicationSettings) : IHandler<UploadFileRequest, Guid>
{
    private readonly long _maxFileUploadSize = applicationSettings.Value.MaxFileUploadSize;

    public async Task<OneOf<Guid, Error>> Process(UploadFileRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Uploading file for file transfer {fileTransferId}", request.FileTransferId);
        var fileTransfer = await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await resourceRightsRepository.CheckUserAccess(fileTransfer.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, request.IsLegacy, cancellationToken);
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
        if (serviceOwner?.StorageProvider is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        };
        var maxUploadSize = resource?.MaxFileTransferSize ?? _maxFileUploadSize;
        if (request.ContentLength > maxUploadSize)
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
            await fileTransferRepository.SetStorageDetails(request.FileTransferId, serviceOwner.StorageProvider.Id, request.FileTransferId.ToString(), request.ContentLength, cancellationToken);
            if (serviceOwner.StorageProvider.Type == StorageProviderType.Altinn3Azure)
            {
                await fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.UploadProcessing, cancellationToken: cancellationToken);
                await eventBus.Publish(AltinnEventType.UploadProcessing, fileTransfer.ResourceId, request.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
            }
            else if (serviceOwner.StorageProvider.Type == StorageProviderType.Azurite) // When running in Azurite storage emulator, there is no async malwarescan that runs before publish
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
