using System.Security.Claims;
using System.Xml;

using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownload;
using Altinn.Broker.Application.ExpireFileTransfer;
using Altinn.Broker.Application.Settings;
using Altinn.Broker.Common;
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
    IAuthorizationService resourceRightsRepository,
    IBackgroundJobClient backgroundJobClient,
    IEventBus eventBus,
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
        var hasAccess = await resourceRightsRepository.CheckAccessAsRecipient(user, fileTransfer, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.FileTransferNotFound;
        };
        if (string.IsNullOrWhiteSpace(fileTransfer?.FileLocation))
        {
            return Errors.NoFileUploaded;
        }
        if (fileTransfer.FileTransferStatusEntity.Status != FileTransferStatus.Published)
        {
            return Errors.FileTransferNotPublished;
        }
        var caller = request.onBehalfOf ?? "0192:" + user?.GetCallerOrganizationId();
        if (fileTransfer.RecipientCurrentStatuses.First(recipientStatus => recipientStatus.Actor.ActorExternalId == caller).Status == ActorFileTransferStatus.DownloadConfirmed)
        {
            return Task.CompletedTask;
        }
        if (!fileTransfer.RecipientCurrentStatuses.Any(recipientStatus => recipientStatus.Actor.ActorExternalId == caller && recipientStatus.Status == ActorFileTransferStatus.DownloadStarted)) //TODO: Replace with DownloadFinished when implemented
        {
            return Errors.ConfirmDownloadBeforeDownloadStarted;
        }
        return await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            await eventBus.Publish(AltinnEventType.DownloadConfirmed, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), caller, cancellationToken);
            await eventBus.Publish(AltinnEventType.DownloadConfirmed, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
            await actorFileTransferStatusRepository.InsertActorFileTransferStatus(request.FileTransferId, ActorFileTransferStatus.DownloadConfirmed, caller, cancellationToken);
            bool shouldConfirmAll = fileTransfer.RecipientCurrentStatuses.Where(recipientStatus => recipientStatus.Actor.ActorExternalId != caller).All(status => status.Status >= ActorFileTransferStatus.DownloadConfirmed);
            if (shouldConfirmAll)
            {
                await eventBus.Publish(AltinnEventType.AllConfirmedDownloaded, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, cancellationToken);
                await fileTransferStatusRepository.InsertFileTransferStatus(request.FileTransferId, FileTransferStatus.AllConfirmedDownloaded);
                var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
                backgroundJobClient.Delete(fileTransfer.HangfireJobId);
                if (resource!.PurgeFileTransferAfterAllRecipientsConfirmed)
                {
                    backgroundJobClient.Enqueue<ExpireFileTransferHandler>((expireFileTransferHandler) => expireFileTransferHandler.Process(new ExpireFileTransferRequest
                    {
                        FileTransferId = request.FileTransferId,
                        Force = true
                    }, null, cancellationToken));
                }
                else
                {
                    var gracePeriod = resource.PurgeFileTransferGracePeriod ?? XmlConvert.ToTimeSpan(ApplicationConstants.DefaultGracePeriod);
                    backgroundJobClient.Schedule<ExpireFileTransferHandler>((expireFileTransferHandler) => expireFileTransferHandler.Process(new ExpireFileTransferRequest
                    {
                        FileTransferId = request.FileTransferId,
                        Force = true
                    }, null, cancellationToken), DateTime.UtcNow.Add(gracePeriod));
                }
            }
            return Task.CompletedTask;
        }, logger, cancellationToken);
    }
}
