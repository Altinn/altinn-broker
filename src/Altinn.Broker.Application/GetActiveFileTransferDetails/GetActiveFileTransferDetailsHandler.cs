using System.Collections.Concurrent;
using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetActiveFileTransferDetails;

public class GetActiveFileTransferDetailsHandler(
    IAuthorizationService authorizationService,
    IFileTransferRepository fileTransferRepository,
    IActorRepository actorRepository,
    IAltinnRegisterService altinnRegisterService,
    IFileTransferStatusRepository fileTransferStatusRepository,
    IResourceRepository resourceRepository,
    IAltinnResourceRepository altinnResourceRepository,
    IFileTransferNotificationRepository fileTransferNotificationRepository,
    ILogger<GetActiveFileTransferDetailsHandler> logger) : IHandler<GetActiveFileTransferDetailsRequest, GetActiveFileTransferDetailsResponse>
{
    public async Task<OneOf<GetActiveFileTransferDetailsResponse, Error>> Process(GetActiveFileTransferDetailsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting active file transfers details for {fileTransferId}", request.FileTransferId);
        var fileTransfer = await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }

        string? caller = request.OnBehalfOf != string.Empty ? request.OnBehalfOf : user?.GetCallerOrganizationId();
        if (caller is null)
        {
            logger.LogError("Caller not found");
            return Errors.NoAccessToResource;
        }

        var callingActor = await actorRepository.GetActorAsync(caller.WithPrefix(), cancellationToken);
        if (callingActor is null)
        {
            return Errors.NoAccessToResource;
        }
        
       var accessToResource = await authorizationService.CheckAccessForSearch(user, fileTransfer.ResourceId, caller, cancellationToken);
        if (!accessToResource)
        {
            return Errors.NoAccessToResource;
        }

        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            logger.LogError("Resource not found for file transfer {fileTransferId}", request.FileTransferId);
            
        }

        var resourceResponse = await altinnResourceRepository.GetResourceMetadata(fileTransfer.ResourceId, cancellationToken);
        var resourceName = resourceResponse?.Title ?? fileTransfer.ResourceId;
        var resourceServiceOwner = resourceResponse?.ServiceOwnerName;

        List<FileTransferActor> fileTransferActors = new List<FileTransferActor>()
        {
            new FileTransferActor()
            {
                Sender = fileTransfer.Sender.ActorExternalId,
                Recipients = fileTransfer.RecipientCurrentStatuses.Select(recipient => recipient.Actor.ActorExternalId).ToList()
            }
        };

        var organizationNameById = new ConcurrentDictionary<string, string?>();
        await Parallel.ForEachAsync(
            fileTransferActors.SelectMany(actor => new[] { actor.Sender }.Concat(actor.Recipients)).Distinct(),
            new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = cancellationToken },
            async (organizationId, ct) =>
            {
                organizationNameById[organizationId] = await altinnRegisterService.LookupOrganizationName(organizationId, ct);
            });
        
        var isSender = fileTransfer.Sender.ActorExternalId == callingActor.ActorExternalId;
        var notificationsByRecipient = fileTransfer.HasNotification
            ? await GetNotificationsByRecipient(fileTransfer, isSender, callingActor, cancellationToken)
            : new Dictionary<string, RecipientNotificationDetails>();

        var recipientDetails = fileTransferActors.SelectMany(actor => actor.Recipients).Distinct().Select(recipientId => new RecipientDetails()
        {
            Recipient = recipientId,
            RecipientName = organizationNameById.GetValueOrDefault(recipientId) ?? recipientId,
            Notification = notificationsByRecipient.GetValueOrDefault(recipientId),
        }).ToList();

        var fileTransferEvents = await fileTransferStatusRepository.GetFileTransferStatusHistory(request.FileTransferId, cancellationToken);
        var initializedEvent = fileTransferEvents.FirstOrDefault(fileTransferEvent => fileTransferEvent.Status == FileTransferStatus.Initialized);
        var publishedEvent = fileTransferEvents.FirstOrDefault(fileTransferEvent => fileTransferEvent.Status == FileTransferStatus.Published);
        var senderOrgNumber = fileTransfer.Sender.ActorExternalId.WithoutPrefix();

        var callerRecipientStatus = fileTransfer.RecipientCurrentStatuses
            .FirstOrDefault(recipientStatus => recipientStatus.Actor.ActorExternalId == callingActor.ActorExternalId);
        var fileTransferStatus = DetermineFileTransferStatus(fileTransfer, callerRecipientStatus);

        return new GetActiveFileTransferDetailsResponse()
        {
            FileTransferId = fileTransfer.FileTransferId,
            ResourceId = fileTransfer.ResourceId,
            ResourceName = resourceName,
            SendersFileTransferReference = fileTransfer.SendersFileTransferReference,
            FileName = fileTransfer.FileName,
            FileTransferSize = fileTransfer.FileTransferSize,
            Checksum = string.IsNullOrWhiteSpace(fileTransfer.Checksum) ? null : fileTransfer.Checksum,
            ChecksumAlgorithm = fileTransfer.ChecksumAlgorithm.ToString(),
            Created = initializedEvent is not null ? FormatTimestamp(initializedEvent.Date) : null,
            Published = publishedEvent is not null ? FormatTimestamp(publishedEvent.Date) : null,
            UseVirusScan = fileTransfer.UseVirusScan,
            Sender = senderOrgNumber,
            SenderName = organizationNameById.GetValueOrDefault(fileTransfer.Sender.ActorExternalId) ?? senderOrgNumber,
            Recipients = recipientDetails,
            PropertyList = fileTransfer.PropertyList,
            ServiceOwner = resourceServiceOwner ?? fileTransfer.ResourceId,
            ExpirationTime = FormatTimestamp(fileTransfer.ExpirationTime),
            Status = fileTransferStatus,
            IsSender = isSender,
            ActorDownloadStatus = callerRecipientStatus?.Status.ToString(),
            HasNotification = fileTransfer.HasNotification
        };
    }

    private static string FormatTimestamp(DateTimeOffset timestamp) => timestamp.ToString("dd.MM.yyyy HH:mm");

    /// <summary>
    /// Each recipient's notification, keyed by the recipient's actor external id. The sender sees the notification status for every recipient, 
    /// while a recipient sees only its own. 
    /// Custom recipients are counted on the recipient they are notified on behalf of, without their addresses.
    /// </summary>
    private async Task<Dictionary<string, RecipientNotificationDetails>> GetNotificationsByRecipient(
        FileTransferEntity fileTransfer,
        bool isSender,
        ActorEntity callingActor,
        CancellationToken cancellationToken)
    {
        var notifications = await fileTransferNotificationRepository.GetNotificationsForFileTransfer(fileTransfer.FileTransferId, cancellationToken);
        var visibleRecipients = fileTransfer.RecipientCurrentStatuses
            .Select(recipientStatus => recipientStatus.Actor)
            .Where(actor => isSender || actor.ActorExternalId == callingActor.ActorExternalId);

        var notificationsByRecipient = new Dictionary<string, RecipientNotificationDetails>();
        foreach (var recipient in visibleRecipients)
        {
            var main = notifications.FirstOrDefault(n => n.ActorId == recipient.ActorId && !n.IsReminder);
            if (main is null)
            {
                continue;
            }
            var reminder = notifications.FirstOrDefault(n => n.ActorId == recipient.ActorId && n.IsReminder);
            var customRecipients = notifications
                .Where(n => n.ActorId is null && !n.IsReminder
                    && n.CustomRecipientRelatedOrganization?.WithoutPrefix() == recipient.ActorExternalId.WithoutPrefix())
                .ToList();

            notificationsByRecipient[recipient.ActorExternalId] = new RecipientNotificationDetails
            {
                Status = main.Status.ToString(),
                SentTime = main.NotificationSent is null ? null : FormatTimestamp(main.NotificationSent.Value),
                Reminder = reminder is null ? null : new NotificationReminderDetails
                {
                    Status = reminder.Status.ToString(),
                    Time = FormatTimestamp(reminder.NotificationSent ?? reminder.RequestedSendTime),
                },
                CustomRecipientCount = customRecipients.Count,
                CustomRecipientsNotifiedCount = customRecipients.Count(n => n.Status == NotificationDeliveryStatus.Sent),
            };
        }
        return notificationsByRecipient;
    }


    /// <summary>
    /// Derives a single frontend-facing status describing what the calling actor should see:
    /// whether the transfer has reached a terminal state (cancelled/purged/failed/all downloaded),
    /// or - while still active - whether the calling actor still needs to download it themselves,
    /// or has already downloaded and is waiting on the remaining recipients.
    /// </summary>
    private static string DetermineFileTransferStatus(FileTransferEntity fileTransfer, ActorFileTransferStatusEntity? callerRecipientStatus)
    {
        switch (fileTransfer.FileTransferStatusEntity.Status)
        {
            case FileTransferStatus.Cancelled:
                return "Cancelled";
            case FileTransferStatus.Purged:
                return "Purged";
            case FileTransferStatus.Failed:
                return "Failed";
            case FileTransferStatus.AllConfirmedDownloaded:
                return "AllDownloaded";
        }

        if (callerRecipientStatus is null)
        {
            return "AwaitingRecipients";
        }

        return callerRecipientStatus.Status == ActorFileTransferStatus.DownloadConfirmed
            ? "AwaitingOtherRecipients"
            : "AwaitingDownloadByCurrentActor";
    }

    private class FileTransferActor
    {
        public string Sender { get; set; } = string.Empty;
        public List<string> Recipients { get; set; } = new List<string>();
    }
}
