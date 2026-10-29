namespace Altinn.Broker.Core.Models.Enums;

/// <summary>
/// Where a notification is on its way to the recipient. Owned by Broker: Altinn Notifications' own statuses
/// (<see cref="NotificationStatusV2"/> and its order statuses) are mapped onto these, so Broker's API does not depend on them.
/// </summary>
public enum NotificationDeliveryStatus
{
    /// <summary>
    /// Created when the file transfer was initialized. Ordered from Altinn Notifications once the file transfer is published.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Accepted by Altinn Notifications, but delivery is not yet confirmed.
    /// </summary>
    Ordered = 1,

    /// <summary>
    /// Refused by Altinn Notifications (422), e.g. because the recipient has no contact information. Never sent, and
    /// no reminder exists either.
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// Delivered to at least one of the recipient's addresses.
    /// </summary>
    Sent = 3,

    /// <summary>
    /// Altinn Notifications finished with it, but it reached none of the recipient's addresses.
    /// </summary>
    NotDelivered = 4,

    /// <summary>
    /// Not sent because its send condition was not met - for a reminder that only goes out while the file has not
    /// been downloaded.
    /// </summary>
    ConditionNotMet = 5,

    /// <summary>
    /// Broker could not confirm the order with Altinn Notifications - every attempt to order it failed (other than a
    /// 422), or the file transfer stopped being available first. It may still have been sent, if Altinn Notifications
    /// created it but its answer never reached Broker.
    /// </summary>
    OrderUnconfirmed = 6,
}
