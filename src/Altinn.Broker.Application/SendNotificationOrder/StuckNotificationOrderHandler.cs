using Altinn.Broker.Application.SendSlackNotification;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Repositories;

using Hangfire;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.SendNotificationOrder;

/// <summary>
/// Picks up notifications still <see cref="NotificationDeliveryStatus.Pending"/> after their file transfer was published,
/// i.e. ones <see cref="SendNotificationOrderHandler"/> kept failing to order until Hangfire gave up on it.
/// Ordering again is safe: Altinn Notifications returns the existing order for an IdempotencyId it already has, so an
/// order it created but whose answer never reached Broker is picked up rather than duplicated.
/// </summary>
public class StuckNotificationOrderHandler(
    IFileTransferNotificationRepository fileTransferNotificationRepository,
    IBackgroundJobClient backgroundJobClient,
    ILogger<StuckNotificationOrderHandler> logger)
{
    /// <summary>
    /// Beyond Hangfire's own retries of the send job (about 8 hours), so a job still retrying is left alone.
    /// </summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(12);

    /// <summary>
    /// After this, a notification would arrive too late to be worth sending, so it is given up on.
    /// </summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromDays(3);

    public async Task Process(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var fileTransfers = await fileTransferNotificationRepository.GetFileTransfersWithPendingNotifications(now - RetryAfter, cancellationToken);
        if (fileTransfers.Count == 0)
        {
            return;
        }
        logger.LogWarning("Found {Count} published file transfer(s) with notifications that were never ordered", fileTransfers.Count);

        foreach (var fileTransfer in fileTransfers)
        {
            // Published is the only status where notifying still makes sense - after it the file transfer is cancelled,
            // downloaded by everyone, purged or failed.
            if (fileTransfer.CurrentStatus is not (FileTransferStatus.Published or null))
            {
                logger.LogWarning(
                    "Giving up on unordered notifications for file transfer {FileTransferId}, which is now {Status}",
                    fileTransfer.FileTransferId,
                    fileTransfer.CurrentStatus);
                await fileTransferNotificationRepository.UpdatePendingStatusesForFileTransfer(
                    fileTransfer.FileTransferId,
                    NotificationDeliveryStatus.OrderUnconfirmed,
                    $"The file transfer was {fileTransfer.CurrentStatus} before the notification could be ordered",
                    cancellationToken);
                continue;
            }

            if (fileTransfer.PublishedAt < now - GiveUpAfter)
            {
                logger.LogError(
                    "Giving up on unordered notifications for file transfer {FileTransferId}, published {PublishedAt}",
                    fileTransfer.FileTransferId,
                    fileTransfer.PublishedAt);
                await fileTransferNotificationRepository.UpdatePendingStatusesForFileTransfer(
                    fileTransfer.FileTransferId,
                    NotificationDeliveryStatus.OrderUnconfirmed,
                    $"Could not be ordered from Altinn Notifications within {GiveUpAfter.TotalDays} days of the file transfer being published",
                    cancellationToken);
                backgroundJobClient.Enqueue<SendSlackNotificationHandler>(handler => handler.Process(
                    "Notification orders given up on",
                    $"File transfer {fileTransfer.FileTransferId} (published {fileTransfer.PublishedAt:u}) has notifications that could not be ordered from Altinn Notifications. Marked as OrderUnconfirmed.",
                    ":warning:"));
                continue;
            }

            logger.LogInformation("Ordering unordered notifications again for file transfer {FileTransferId}", fileTransfer.FileTransferId);
            backgroundJobClient.Enqueue<SendNotificationOrderHandler>(handler => handler.Process(fileTransfer.FileTransferId, CancellationToken.None));
        }
    }
}
