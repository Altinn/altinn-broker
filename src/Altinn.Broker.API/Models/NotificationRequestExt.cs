using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Broker.Enums;

namespace Altinn.Broker.Models;

/// <summary>
/// Requests a notification be sent to the file transfer's recipient(s) once it is published.
/// </summary>
public class NotificationRequestExt
{
    [JsonPropertyName("notificationTemplate")]
    [Required]
    public NotificationTemplateExt NotificationTemplate { get; set; }

    [JsonPropertyName("emailSubject")]
    [StringLength(512)]
    public string? EmailSubject { get; set; }

    [JsonPropertyName("emailBody")]
    [StringLength(10000)]
    public string? EmailBody { get; set; }

    [JsonPropertyName("emailContentType")]
    public EmailContentTypeExt EmailContentType { get; set; } = EmailContentTypeExt.Plain;

    [JsonPropertyName("smsBody")]
    [StringLength(2144)]
    public string? SmsBody { get; set; }

    /// <summary>
    /// Whether a reminder should be sent if the main notification does not lead to the file transfer being
    /// confirmed/downloaded.
    /// </summary>
    [JsonPropertyName("sendReminder")]
    public bool SendReminder { get; set; }

    [JsonPropertyName("reminderEmailSubject")]
    [StringLength(512)]
    public string? ReminderEmailSubject { get; set; }

    [JsonPropertyName("reminderEmailBody")]
    [StringLength(10000)]
    public string? ReminderEmailBody { get; set; }

    [JsonPropertyName("reminderEmailContentType")]
    public EmailContentTypeExt? ReminderEmailContentType { get; set; }

    [JsonPropertyName("reminderSmsBody")]
    [StringLength(2144)]
    public string? ReminderSmsBody { get; set; }

    [JsonPropertyName("notificationChannel")]
    [Required]
    public NotificationChannelExt NotificationChannel { get; set; }

    [JsonPropertyName("reminderNotificationChannel")]
    public NotificationChannelExt? ReminderNotificationChannel { get; set; }

    /// <summary>
    /// Language to use when <see cref="NotificationTemplate"/> is "GenericAltinnMessage". One of "nb"/"nn"/"en" -
    /// defaults to "nb" when not set.
    /// </summary>
    [JsonPropertyName("language")]
    [RegularExpression("^(nb|nn|en)$", ErrorMessage = "Language must be one of 'nb', 'nn' or 'en'")]
    public string? Language { get; set; }

    /// <summary>
    /// Additional recipients to notify, beyond the file transfer's own recipients. Combined with the file
    /// transfer's own recipients.
    /// </summary>
    [JsonPropertyName("customRecipients")]
    public List<NotificationRecipientExt>? CustomRecipients { get; set; }
}
