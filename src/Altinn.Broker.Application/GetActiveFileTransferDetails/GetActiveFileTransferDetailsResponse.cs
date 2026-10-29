namespace Altinn.Broker.Application.GetActiveFileTransferDetails;

public class GetActiveFileTransferDetailsResponse
{
    public Guid? FileTransferId { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string? SendersFileTransferReference { get; set; }
    public string? FileName { get; set; }
    public long? FileTransferSize { get; set; }
    public string? Checksum { get; set; }
    public string? ChecksumAlgorithm { get; set; }
    public string? Created { get; set; }
    public bool? UseVirusScan { get; set; }
    public string? Published { get; set; }
    public string? Sender { get; set; }
    public string? SenderName { get; set; }
    public  List<RecipientDetails>? Recipients { get; set; }
    public Dictionary<string, string>? PropertyList { get; set; }
    public string? ServiceOwner { get; set; }
    public string? ExpirationTime { get; set; }
    public string? Status { get; set; }
    public bool? IsSender { get; set; }
    public string? ActorDownloadStatus { get; set; }
    public bool? HasNotification { get; set; }
}

public class RecipientDetails
{
    public required string Recipient { get; set; }
    public string? RecipientName { get; set; }

    /// <summary>
    /// How the notification to this recipient is going. Null when no notification was ordered for it - and, for a
    /// caller who is a recipient, on every recipient but the caller itself.
    /// </summary>
    public RecipientNotificationDetails? Notification { get; set; }
}

public class RecipientNotificationDetails
{
    /// <summary>
    /// The notification's NotificationDeliveryStatus: Pending, Ordered, Rejected, Sent, NotDelivered, ConditionNotMet
    /// or OrderUnconfirmed.
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// When delivery was confirmed, once it is Sent.
    /// </summary>
    public string? SentTime { get; set; }

    /// <summary>
    /// Null when no reminder was ordered, or not yet - the reminder is created once the notification is ordered.
    /// </summary>
    public NotificationReminderDetails? Reminder { get; set; }

    /// <summary>
    /// How many custom recipients are notified on behalf of this recipient, and how many of them have been. Only the
    /// counts - their addresses are not exposed.
    /// </summary>
    public int CustomRecipientCount { get; set; }

    public int CustomRecipientsNotifiedCount { get; set; }
}

public class NotificationReminderDetails
{
    /// <summary>
    /// The reminder's NotificationDeliveryStatus.
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// When it was delivered once Sent, otherwise when it is planned to go out.
    /// </summary>
    public string? Time { get; set; }
}