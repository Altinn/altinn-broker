using Altinn.Broker.Application.SendSlackNotification;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Repositories;

using Hangfire;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.CheckNotificationDelivery;

public class CheckNotificationDeliveryHandler(
    IFileTransferNotificationRepository fileTransferNotificationRepository,
    IAltinnNotificationService altinnNotificationService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<CheckNotificationDeliveryHandler> logger)
{
    private static readonly int[] PollBackoffSeconds = 
    [60,
    2 * 60,
    4 * 60,
    8 * 60,
    15 * 60, 
    4 * 60 * 60, 
    8 * 60 * 60, 
    12 * 60 * 60, 
    16 * 60 * 60, 
    36 * 60 * 60, 
    48 * 60 * 60, 
    72 * 60 * 60];
    public static readonly int MaxAttempts = PollBackoffSeconds.Length + 1;
    private const string OrderSendConditionNotMet = "Order_SendConditionNotMet";
    private static readonly string[] FinalOrderStatuses = ["Order_Completed", OrderSendConditionNotMet, "Order_Cancelled"];

    public async Task Process(Guid notificationId, CancellationToken cancellationToken, int attempt = 1)
    {
        var notification = await fileTransferNotificationRepository.GetNotificationById(notificationId, cancellationToken);
        if (notification is null)
        {
            logger.LogError("Notification {NotificationId} not found when checking delivery status", notificationId);
            return;
        }
        if (notification.NotificationSent is not null)
        {
            return;
        }
        if (notification.ShipmentId is null)
        {
            logger.LogError("Notification {NotificationId} has no shipment id when checking delivery status", notificationId);
            return;
        }

        var statusResponse = await altinnNotificationService.GetNotificationDetailsV2(notification.ShipmentId.Value.ToString(), cancellationToken);
        if (!FinalOrderStatuses.Contains(statusResponse.Status))
        {
            if (attempt >= MaxAttempts)
            {
                logger.LogWarning(
                    "Gave up polling delivery status for notification {NotificationId} after {Attempts} attempts, last status {Status}",
                    notificationId,
                    attempt,
                    statusResponse.Status);
                backgroundJobClient.Enqueue<SendSlackNotificationHandler>(handler => handler.Process(
                    "Notification delivery status polling gave up",
                    $"Notification {notificationId} (file transfer {notification.FileTransferId}) did not reach a final status after {attempt} attempts. Last status: {statusResponse.Status}",
                    ":warning:"));
                return;
            }

            var delaySeconds = PollBackoffSeconds[attempt - 1];
            backgroundJobClient.Schedule<CheckNotificationDeliveryHandler>(
                handler => handler.Process(notificationId, CancellationToken.None, attempt + 1),
                TimeSpan.FromSeconds(delaySeconds));
            return;
        }

        if (statusResponse.Status == OrderSendConditionNotMet)
        {
            await fileTransferNotificationRepository.UpdateStatus(notificationId, NotificationDeliveryStatus.ConditionNotMet, null, cancellationToken);
            return;
        }

        var sentRecipients = statusResponse.Recipients.Where(r => r.IsSent()).ToList();
        var failedRecipients = statusResponse.Recipients.Where(r => r.Status.IsFailed()).ToList();
        if (failedRecipients.Count > 0)
        {
            logger.LogWarning(
                "Notification {NotificationId} had {FailedCount} failed recipient(s): {Statuses}",
                notificationId,
                failedRecipients.Count,
                string.Join(", ", failedRecipients.Select(r => r.Status)));
        }

        if (sentRecipients.Count == 0)
        {
            logger.LogError("Notification {NotificationId} reached final status {Status} with no successfully delivered recipients", notificationId, statusResponse.Status);
            var reason = failedRecipients.Count > 0
                ? string.Join(", ", failedRecipients.Select(r => r.Status).Distinct())
                : statusResponse.Status;
            await fileTransferNotificationRepository.UpdateStatus(notificationId, NotificationDeliveryStatus.NotDelivered, reason, cancellationToken);
            return;
        }

        var sentTime = sentRecipients.Min(r => r.LastUpdate);
        var notificationAddress = string.Join(", ", sentRecipients.Select(r => r.Destination));
        await fileTransferNotificationRepository.UpdateNotificationSent(notificationId, sentTime, notificationAddress, cancellationToken);
    }
}
