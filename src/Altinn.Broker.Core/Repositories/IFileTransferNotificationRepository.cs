using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IFileTransferNotificationRepository
{
    Task AddNotification(BrokerNotificationEntity notification, CancellationToken cancellationToken);

    Task<List<BrokerNotificationEntity>> GetNotificationsForFileTransfer(Guid fileTransferId, CancellationToken cancellationToken);

    Task<bool> HasNotificationsForFileTransfer(Guid fileTransferId, CancellationToken cancellationToken);

    Task<BrokerNotificationEntity?> GetNotificationById(Guid notificationId, CancellationToken cancellationToken);

    Task UpdateOrderResponseData(Guid notificationId, Guid notificationOrderId, Guid shipmentId, CancellationToken cancellationToken);

    Task UpdateNotificationSent(Guid notificationId, DateTimeOffset sentTime, string notificationAddress, CancellationToken cancellationToken);
}
