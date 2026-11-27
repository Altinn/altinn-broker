using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models.Enums;

namespace Altinn.Broker.Core.Repositories;

public interface IFileTransferNotificationRepository
{
    Task AddNotification(BrokerNotificationEntity notification, CancellationToken cancellationToken);

    Task<List<BrokerNotificationEntity>> GetNotificationsForFileTransfer(Guid fileTransferId, CancellationToken cancellationToken);

    Task<BrokerNotificationEntity?> GetNotificationById(Guid notificationId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores Altinn Notifications' ids for an accepted order and marks it <see cref="NotificationDeliveryStatus.Ordered"/>.
    /// </summary>
    Task UpdateOrderResponseData(Guid notificationId, Guid notificationOrderId, Guid shipmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores when and where it was delivered and marks it <see cref="NotificationDeliveryStatus.Sent"/>.
    /// </summary>
    Task UpdateNotificationSent(Guid notificationId, DateTimeOffset sentTime, string notificationAddress, CancellationToken cancellationToken);

    /// <summary>
    /// For the outcomes that carry no order ids or delivery data: rejected, not delivered, or condition not met.
    /// </summary>
    Task UpdateStatus(Guid notificationId, NotificationDeliveryStatus status, string? statusReason, CancellationToken cancellationToken);

    /// <summary>
    /// Published file transfers, published before <paramref name="publishedBefore"/>, that still have
    /// <see cref="NotificationDeliveryStatus.Pending"/> notifications.
    /// </summary>
    Task<List<FileTransferWithPendingNotifications>> GetFileTransfersWithPendingNotifications(DateTimeOffset publishedBefore, CancellationToken cancellationToken);

    /// <summary>
    /// Moves every notification of the file transfer that is still <see cref="NotificationDeliveryStatus.Pending"/> to
    /// <paramref name="status"/>; the others are left as they are.
    /// </summary>
    Task UpdatePendingStatusesForFileTransfer(Guid fileTransferId, NotificationDeliveryStatus status, string? statusReason, CancellationToken cancellationToken);
}
