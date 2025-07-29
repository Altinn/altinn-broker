using System.Security.Claims;
using System.Xml;

using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownload;
using Altinn.Broker.Application.PurgeFileTransfer;
using Altinn.Broker.Application.Middlewares;
using Altinn.Broker.Application.Settings;
using Altinn.Broker.Common;
using Altinn.Broker.Core;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

public class ConfirmDownloadHandler(
    IFileTransferRepository fileTransferRepository,
    IFileTransferStatusRepository fileTransferStatusRepository,
    IActorFileTransferStatusRepository actorFileTransferStatusRepository,
    IResourceRepository resourceRepository,
    IAuthorizationService authorizationService,
    IBackgroundJobClient backgroundJobClient,
    EventBusMiddleware eventBus,
    ILogger<ConfirmDownloadHandler> logger) : IHandler<ConfirmDownloadRequest, Task>
{
    public async Task<OneOf<Task, Error>> Process(ConfirmDownloadRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Confirming download for file transfer {fileTransferId}", request.FileTransferId);
        var fileTransfer = await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await authorizationService.CheckAccessAsRecipient(user, fileTransfer, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        if (request.IsLegacy && request.OnBehalfOfConsumer is not null && !fileTransfer.IsRecipient(request.OnBehalfOfConsumer))
        {
            return Errors.NoAccessToResource;
        }
        if (string.IsNullOrWhiteSpace(fileTransfer?.FileLocation))
        {
            return Errors.NoFileUploaded;
        }
        if (fileTransfer.FileTransferStatusEntity.Status != FileTransferStatus.Published)
        {
            return Errors.FileTransferNotPublished;
        }
        var caller = (request.OnBehalfOfConsumer ?? user?.GetCallerOrganizationId())?.WithPrefix();
        if (string.IsNullOrWhiteSpace(caller))
        {
            logger.LogError("Caller is not set");
            return Errors.NoAccessToResource;
        }
        if (fileTransfer.RecipientCurrentStatuses.First(recipientStatus => recipientStatus.Actor.ActorExternalId == caller).Status == ActorFileTransferStatus.DownloadConfirmed)
        {
            return Task.CompletedTask;
        }
        if (!fileTransfer.RecipientCurrentStatuses.Any(recipientStatus => recipientStatus.Actor.ActorExternalId == caller && recipientStatus.Status == ActorFileTransferStatus.DownloadStarted)) //TODO: Replace with DownloadFinished when implemented
        {
            return Errors.ConfirmDownloadBeforeDownloadStarted;
        }
        bool shouldConfirmAll = fileTransfer.RecipientCurrentStatuses.Where(recipientStatus => recipientStatus.Actor.ActorExternalId != caller).All(status => status.Status >= ActorFileTransferStatus.DownloadConfirmed);
        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.DownloadConfirmed, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), caller, Guid.NewGuid()));
            backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.DownloadConfirmed, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, Guid.NewGuid()));
            await actorFileTransferStatusRepository.InsertActorFileTransferStatus(request.FileTransferId, ActorFileTransferStatus.DownloadConfirmed, caller, cancellationToken);
            if (shouldConfirmAll)
            {
                backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.AllConfirmedDownloaded, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, Guid.NewGuid()));
                await fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.AllConfirmedDownloaded);
                if (resource!.PurgeFileTransferAfterAllRecipientsConfirmed)
                {
                    var gracePeriod = resource.PurgeFileTransferGracePeriod ?? XmlConvert.ToTimeSpan(ApplicationConstants.DefaultGracePeriod);
                    backgroundJobClient.Schedule<PurgeFileTransferHandler>((expireFileTransferHandler) => expireFileTransferHandler.Process(new PurgeFileTransferRequest
                    {
                        FileTransferId = request.FileTransferId,
                        PurgeTrigger = PurgeTrigger.AllConfirmedDownloaded
                    }, null, cancellationToken), DateTime.UtcNow.Add(gracePeriod));
                }
            }
            return Task.CompletedTask;
        }, logger, cancellationToken);
        if (shouldConfirmAll)
        {
            backgroundJobClient.Delete(fileTransfer.HangfireJobId); // Performed outside of transaction to avoid issue with Hangfire distributed lock implementation
        }
        return Task.CompletedTask;
    }
}
