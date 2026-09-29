using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Models.Notifications;

namespace Altinn.Broker.Core.Domain;

public class BrokerNotificationEntity
{
    public required Guid Id { get; set; }

    public Guid? NotificationOrderId { get; set; }

    public Guid? ShipmentId { get; set; }

    public required NotificationTemplate NotificationTemplate { get; set; }

    public required NotificationChannel NotificationChannel { get; set; }

    public required DateTimeOffset RequestedSendTime { get; set; }

    public required Guid FileTransferId { get; set; }

    /// <summary>
    /// The real file-transfer actor this notification is for, or null when <see cref="CustomRecipient"/> is set instead.
    /// </summary>
    public long? ActorId { get; set; }

    /// <summary>
    /// The recipient (serialized), when this row is for an arbitrary custom recipient rather than a real file-transfer
    /// actor. Exactly one of <see cref="ActorId"/>/<see cref="CustomRecipient"/> is set - enforced at the DB level too.
    /// </summary>
    public string? CustomRecipient { get; set; }

    public required DateTimeOffset Created { get; set; }

    public bool IsReminder { get; set; }

    public DateTimeOffset? NotificationSent { get; set; }

    public string? NotificationAddress { get; set; }

    public string? OrderRequest { get; set; }
}
