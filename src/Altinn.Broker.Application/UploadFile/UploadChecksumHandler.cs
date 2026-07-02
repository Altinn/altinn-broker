using Altinn.Broker.Application.Middlewares;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.UploadFile;

public class UploadChecksumHandler(
    IFileTransferRepository fileTransferRepository,
    IFileTransferStatusRepository fileTransferStatusRepository,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IBrokerStorageService brokerStorageService,
    IBackgroundJobClient backgroundJobClient,
    EventBusMiddleware eventBus,
    ILogger<UploadChecksumHandler> logger)
{
    public async Task<bool> Process(Guid fileTransferId, CancellationToken cancellationToken)
    {
        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            logger.LogWarning("Checksum processing skipped because file transfer {fileTransferId} was not found", fileTransferId);
            return false;
        }

        if (fileTransfer.FileTransferStatusEntity.Status is FileTransferStatus.Failed or FileTransferStatus.Published
            && !string.IsNullOrWhiteSpace(fileTransfer.Checksum))
        {
            return true;
        }

        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            logger.LogError("Checksum processing failed because resource {resourceId} was not found", fileTransfer.ResourceId);
            return false;
        }

        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            logger.LogError("Checksum processing failed because service owner {serviceOwnerId} was not found", resource.ServiceOwnerId);
            return false;
        }

        var computedChecksum = await brokerStorageService.ComputeDestinationBlobChecksumAsync(
            serviceOwner,
            fileTransfer,
            cancellationToken);
        if (computedChecksum is null)
        {
            logger.LogError("Checksum processing failed because blob checksum could not be computed for {fileTransferId}", fileTransferId);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(fileTransfer.Checksum)
            && !string.Equals(computedChecksum, fileTransfer.Checksum, StringComparison.InvariantCultureIgnoreCase))
        {
            await FailChecksumMismatchAsync(fileTransfer, serviceOwner, cancellationToken);
            return false;
        }

        if (string.IsNullOrWhiteSpace(fileTransfer.Checksum))
        {
            await fileTransferRepository.SetChecksum(fileTransferId, computedChecksum, cancellationToken);
            fileTransfer.Checksum = computedChecksum;
        }

        await brokerStorageService.SetContentHashForExistingBlob(serviceOwner, fileTransfer, cancellationToken);
        return true;
    }

    private async Task FailChecksumMismatchAsync(
        FileTransferEntity fileTransfer,
        ServiceOwnerEntity serviceOwner,
        CancellationToken cancellationToken)
    {
        await fileTransferStatusRepository.InsertFileTransferStatus(
            fileTransfer.FileTransferId,
            FileTransferStatus.Failed,
            timestamp: DateTime.UtcNow,
            detailedFileTransferStatus: "Checksum mismatch",
            cancellationToken: cancellationToken);
        backgroundJobClient.Enqueue<IBrokerStorageService>(service =>
            service.DeleteFile(serviceOwner, fileTransfer, cancellationToken));
        backgroundJobClient.Enqueue(() => eventBus.Publish(
            AltinnEventType.UploadFailed,
            fileTransfer.ResourceId,
            fileTransfer.FileTransferId.ToString(),
            fileTransfer.Sender.ActorExternalId,
            Guid.NewGuid(),
            AltinnEventSubjectRole.Sender));
    }
}
