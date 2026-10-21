using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.CheckNotificationCondition;

/// <summary>
/// Answers the condition endpoint Altinn Notification calls right before sending a notification or a reminder. A
/// notification is for one recipient of one file transfer, so this decides per recipient: once they have confirmed the
/// download there is nothing left to remind them about.
/// </summary>
public class CheckNotificationConditionHandler(
    IFileTransferNotificationRepository fileTransferNotificationRepository,
    IFileTransferRepository fileTransferRepository,
    ILogger<CheckNotificationConditionHandler> logger) : IHandler<Guid, CheckNotificationConditionResponse>
{
    private static readonly FileTransferStatus[] StatusesWithNothingToNotifyAbout =
    [
        FileTransferStatus.Cancelled,
        FileTransferStatus.Purged,
        FileTransferStatus.Failed
    ];

    public async Task<OneOf<CheckNotificationConditionResponse, Error>> Process(Guid notificationId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var notification = await fileTransferNotificationRepository.GetNotificationById(notificationId, cancellationToken);
        if (notification is null)
        {
            logger.LogWarning("Condition checked for unknown notification {NotificationId}. Answering that it should not be sent.", notificationId);
            return DoNotSend();
        }

        var fileTransfer = await fileTransferRepository.GetFileTransfer(notification.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            logger.LogWarning(
                "Condition checked for notification {NotificationId} whose file transfer {FileTransferId} no longer exists. Answering that it should not be sent.",
                notificationId,
                notification.FileTransferId);
            return DoNotSend();
        }

        if (StatusesWithNothingToNotifyAbout.Contains(fileTransfer.FileTransferStatusEntity.Status))
        {
            logger.LogInformation(
                "Not sending notification {NotificationId} because file transfer {FileTransferId} is {Status}",
                notificationId,
                notification.FileTransferId,
                fileTransfer.FileTransferStatusEntity.Status);
            return DoNotSend();
        }

        var recipientStatus = FindRecipientStatus(fileTransfer, notification);
        if (recipientStatus is null)
        {
            logger.LogWarning(
                "Could not find the recipient of notification {NotificationId} among the recipients of file transfer {FileTransferId}. Answering that it should be sent.",
                notificationId,
                notification.FileTransferId);
            return Send();
        }

        if (recipientStatus.Status == ActorFileTransferStatus.DownloadConfirmed)
        {
            logger.LogInformation(
                "Not sending notification {NotificationId} because the recipient has confirmed the download of file transfer {FileTransferId}",
                notificationId,
                notification.FileTransferId);
            return DoNotSend();
        }

        return Send();
    }

    /// <summary>
    /// The notification is either for a recipient of the file transfer, identified by actor, or for a custom recipient
    /// notified on behalf of one of them, identified by organization number.
    /// </summary>
    private static ActorFileTransferStatusEntity? FindRecipientStatus(FileTransferEntity fileTransfer, BrokerNotificationEntity notification)
    {
        if (notification.ActorId is not null)
        {
            return fileTransfer.RecipientCurrentStatuses.FirstOrDefault(status => status.Actor.ActorId == notification.ActorId);
        }
        if (string.IsNullOrEmpty(notification.CustomRecipientRelatedOrganization))
        {
            return null;
        }
        var relatedOrganization = notification.CustomRecipientRelatedOrganization.WithoutPrefix();
        return fileTransfer.RecipientCurrentStatuses.FirstOrDefault(status => status.Actor.ActorExternalId.WithoutPrefix() == relatedOrganization);
    }

    private static CheckNotificationConditionResponse Send() => new() { SendNotification = true };

    private static CheckNotificationConditionResponse DoNotSend() => new() { SendNotification = false };
}
