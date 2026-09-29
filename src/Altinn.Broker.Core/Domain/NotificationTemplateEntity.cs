using Altinn.Broker.Core.Models.Enums;

namespace Altinn.Broker.Core.Domain;

/// <summary>
/// A localized, fixed notification message for <see cref="NotificationTemplate.GenericAltinnMessage"/>. The
/// caller's own free text (if any) is embedded into the {textToken} placeholder within the body/SMS fields.
/// </summary>
public class NotificationTemplateEntity
{
    public required int Id { get; set; }

    public required NotificationTemplate NotificationTemplate { get; set; }

    public required string Language { get; set; }

    public string? EmailSubject { get; set; }

    public string? EmailBody { get; set; }

    public string? SmsBody { get; set; }

    public string? ReminderEmailSubject { get; set; }

    public string? ReminderEmailBody { get; set; }

    public string? ReminderSmsBody { get; set; }
}
