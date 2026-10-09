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
    /// The file-transfer actor this notification is for, or null when this row is for a custom recipient instead.
    /// </summary>
    public long? ActorId { get; set; }

    /// <summary>
    /// Set together with <see cref="CustomRecipientIdentifier"/> when this row is for a custom recipient rather than a
    /// file-transfer actor. Either <see cref="ActorId"/> or both custom recipient fields are set - enforced at the DB
    /// level too.
    /// </summary>
    public CustomRecipientType? CustomRecipientType { get; set; }

    /// <summary>
    /// The custom recipient's identifier, as given by <see cref="CustomRecipientType"/>: organization numbers as
    /// "0192:123456789", national identity numbers as "urn:altinn:person:identifier-no:12345678901", email addresses
    /// and mobile numbers as given.
    /// </summary>
    public string? CustomRecipientIdentifier { get; set; }

    /// <summary>
    /// The organization number (as "0192:123456789") of the file transfer recipient the custom recipient is notified
    /// on behalf of. Set whenever <see cref="CustomRecipientIdentifier"/> is.
    /// </summary>
    public string? CustomRecipientRelatedOrganization { get; set; }

    public required DateTimeOffset Created { get; set; }

    public bool IsReminder { get; set; }

    public DateTimeOffset? NotificationSent { get; set; }

    public string? NotificationAddress { get; set; }

    public string? OrderRequest { get; set; }
}
