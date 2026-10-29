using System.Text.Json.Serialization;


namespace Altinn.Broker.Models;

/// <summary>
/// Requests a notification be sent to the file transfer's recipient(s) once it is published. The notification text
/// is always Altinn's standard file transfer message - the caller only decides how and to whom it is sent.
/// </summary>
public class NotificationRequestExt
{
    /// <summary>
    /// Whether a reminder should be sent if the main notification does not lead to the file transfer being
    /// confirmed/downloaded.
    /// </summary>
    [JsonPropertyName("sendReminder")]
    public bool SendReminder { get; set; }

    /// <summary>
    /// Additional recipients to notify, beyond the file transfer's own recipients. Combined with the file
    /// transfer's own recipients.
    /// </summary>
    [JsonPropertyName("customRecipients")]
    public List<NotificationRecipientExt>? CustomRecipients { get; set; }
}
