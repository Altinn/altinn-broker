using System.Security.Claims;

using Altinn.Broker.Application.Middlewares;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.PurgeFileTransfer;
public class PurgeFileTransferHandler(IFileTransferRepository fileTransferRepository, IFileTransferStatusRepository fileTransferStatusRepository, IServiceOwnerRepository serviceOwnerRepository, IBrokerStorageService brokerStorageService, IResourceRepository resourceRepository, EventBusMiddleware eventBus, IBackgroundJobClient backgroundJobClient, ILogger<PurgeFileTransferHandler> logger) : IHandler<PurgeFileTransferRequest, Task>
{
    [AutomaticRetry(Attempts = 0)]
    public async Task<OneOf<Task, Error>> Process(PurgeFileTransferRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Deleting file transfer with id {fileTransferId} because {purgeTrigger}", request.FileTransferId.ToString(), request.PurgeTrigger.ToString());
        var fileTransfer = await GetFileTransferAsync(request.FileTransferId, cancellationToken);
        var resource = await GetResource(fileTransfer.ResourceId, cancellationToken);
        var serviceOwner = await GetServiceOwnerAsync(resource.ServiceOwnerId);

        if (fileTransfer.FileTransferStatusEntity.Status == Core.Domain.Enums.FileTransferStatus.Purged)
        {
            logger.LogInformation("FileTransfer has already been set to purged");
        }
        if (
            fileTransfer.FileTransferStatusEntity.Status == Core.Domain.Enums.FileTransferStatus.AllConfirmedDownloaded 
            && request.PurgeTrigger == PurgeTrigger.FileTransferExpiry
            && resource!.PurgeFileTransferAfterAllRecipientsConfirmed)
        {
            logger.LogInformation("File transfer will be purged as part of the PurgeFileTransferAfterAllRecipientsConfirmed process.");
            return Task.CompletedTask;
        }
        await brokerStorageService.DeleteFile(serviceOwner, fileTransfer, cancellationToken); // This must be idempotent - i.e not fail on file not existing
        if (request.PurgeTrigger != PurgeTrigger.MalwareScanFailed)
        {
            await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
            {
                await fileTransferStatusRepository.InsertFileTransferStatus(fileTransfer.FileTransferId, Core.Domain.Enums.FileTransferStatus.Purged, cancellationToken: cancellationToken);
                backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.FilePurged, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, Guid.NewGuid()));
                return Task.CompletedTask;
            }, logger, cancellationToken);
        }
        return TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            var recipientsWhoHaveNotDownloaded = fileTransfer.RecipientCurrentStatuses.Where(latestStatus => latestStatus.Status < Core.Domain.Enums.ActorFileTransferStatus.DownloadConfirmed).ToList();
            foreach (var recipient in recipientsWhoHaveNotDownloaded)
            {
                logger.LogError("Recipient {recipientExternalReference} did not download the fileTransfer with id {fileTransferId}", recipient.Actor.ActorExternalId, recipient.FileTransferId.ToString());
                backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.FileNeverConfirmedDownloaded, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), recipient.Actor.ActorExternalId, Guid.NewGuid()));
            }
            if (recipientsWhoHaveNotDownloaded.Count > 0) backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.FileNeverConfirmedDownloaded, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, Guid.NewGuid()));
            return Task.CompletedTask;
    }, logger, cancellationToken);
    }
    [AutomaticRetry(Attempts = 0)]

    private async Task<FileTransferEntity> GetFileTransferAsync(Guid fileTransferId, CancellationToken cancellationToken)
    {
        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            throw new Exception("FileTransfer not found");
        }
        return fileTransfer;
    }
    private async Task<ServiceOwnerEntity> GetServiceOwnerAsync(string serviceOwnerId)
    {
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(serviceOwnerId);
        if (serviceOwner is null)
        {
            throw new Exception("ServiceOwner not found");
        }
        return serviceOwner;
    }
    private async Task<ResourceEntity> GetResource(string resourceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            throw new ArgumentNullException(nameof(resourceId), "Resource ID cannot be null or empty");
        }

        var resource = await resourceRepository.GetResource(resourceId, cancellationToken);
        if (resource is null)
        {
            throw new Exception("Resource " + resourceId + " not found in Broker Resource store");
        }
        return resource;
    }

}
