using System.Security.Claims;

using Altinn.Broker.Application.Middlewares;
using Altinn.Broker.Common;
using Altinn.Broker.Core;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.UploadFile;

public class UploadFileHandler(
    TusUploadValidationService tusUploadValidationService,
    IFileTransferStatusRepository fileTransferStatusRepository,
    IFileTransferRepository fileTransferRepository,
    IBrokerStorageService brokerStorageService,
    CompleteFileUploadHandler completeFileUploadHandler,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IBackgroundJobClient backgroundJobClient,
    EventBusMiddleware eventBus,
    ILogger<UploadFileHandler> logger) : IHandler<UploadFileRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(UploadFileRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Uploading file for file transfer {fileTransferId}", request.FileTransferId);

        var (fileTransfer, _, validationError) = await tusUploadValidationService.ValidateForUploadAsync(
            user,
            request.FileTransferId,
            request.ContentLength,
            request.IsLegacy,
            cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        if (request.IsLegacy && request.OnBehalfOfConsumer is not null && !fileTransfer.IsSender(request.OnBehalfOfConsumer))
        {
            return Errors.NoAccessToResource;
        }

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

        var uploadStartedTimestamp = DateTime.UtcNow;
        var uploaderVendor = user?.GetCallerVendorId()?.WithPrefix();
        await fileTransferStatusRepository.InsertFileTransferStatus(
            request.FileTransferId,
            FileTransferStatus.UploadStarted,
            timestamp: uploadStartedTimestamp,
            vendor: uploaderVendor,
            cancellationToken: cancellationToken);

        try
        {
            var result = await brokerStorageService.UploadFile(serviceOwner, fileTransfer, request.UploadStream, cancellationToken);
            if (result is null)
            {
                return await TransactionWithRetriesPolicy.Execute(async ct =>
                {
                    await fileTransferStatusRepository.InsertFileTransferStatus(
                        request.FileTransferId,
                        FileTransferStatus.Failed,
                        timestamp: DateTime.UtcNow,
                        detailedFileTransferStatus: "File upload failed and was aborted",
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

            var (checksum, uploadLength) = result.Value;
            var storageProvider = serviceOwner.GetStorageProvider(fileTransfer.UseVirusScan);
            if (storageProvider is null)
            {
                throw new InvalidOperationException(Errors.StorageProviderNotReady.Message);
            }
            await fileTransferRepository.SetStorageDetails(
                request.FileTransferId,
                storageProvider.Id,
                request.FileTransferId.ToString(),
                uploadLength,
                CancellationToken.None);
            if (fileTransfer.Checksum is null && !string.IsNullOrWhiteSpace(checksum))
            {
                await fileTransferRepository.SetChecksum(
                    request.FileTransferId,
                    checksum,
                    CancellationToken.None);
            }
            return await completeFileUploadHandler.Process(
                new CompleteFileUploadRequest
                {
                    FileTransferId = request.FileTransferId,
                    Checksum = checksum,
                    UploadLength = uploadLength
                },
                user,
                cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(
                "Unexpected error occurred while uploading file: {errorMessage} \nStack trace: {stackTrace}",
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
    }
}
