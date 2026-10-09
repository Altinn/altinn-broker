using Altinn.Broker.Core.Models.Notifications;

namespace Altinn.Broker.Core.Repositories;

public interface IAltinnNotificationService
{
    Task<NotificationOrderResponseV2?> CreateNotificationV2(NotificationOrderRequestV2 notificationRequest, CancellationToken cancellationToken = default);
    Task<bool> CancelNotification(string orderId, CancellationToken cancellationToken = default);
    Task<NotificationStatusResponseV2> GetNotificationDetailsV2(string shipmentId, CancellationToken cancellationToken = default);
}

