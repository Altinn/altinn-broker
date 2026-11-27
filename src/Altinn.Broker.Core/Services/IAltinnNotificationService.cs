using Altinn.Broker.Core.Models.Notifications;
using Altinn.Broker.Core.Services;

namespace Altinn.Broker.Core.Repositories;

public interface IAltinnNotificationService
{
    /// <summary>
    /// Throws <see cref="NotificationOrderRejectedException"/> when Altinn Notifications refuses the order (422), and
    /// another exception for any other failure.
    /// </summary>
    Task<NotificationOrderResponseV2> CreateNotificationV2(NotificationOrderRequestV2 notificationRequest, CancellationToken cancellationToken = default);
    Task<bool> CancelNotification(string orderId, CancellationToken cancellationToken = default);
    Task<NotificationStatusResponseV2> GetNotificationDetailsV2(string shipmentId, CancellationToken cancellationToken = default);
}

