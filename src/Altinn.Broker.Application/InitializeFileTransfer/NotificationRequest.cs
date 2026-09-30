using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models.Notifications;
using Altinn.Broker.Core.Models.Enums;

namespace Altinn.Broker.Application.InitializeFileTransfer
{
    public class NotificationRequest
    {
        public required NotificationTemplate NotificationTemplate { get; set; }

        public string? EmailSubject { get; set; }

        public string? EmailBody { get; set; }

        public EmailContentType EmailContentType { get; set; } = EmailContentType.Plain;

        public string? SmsBody { get; set; }

        public bool SendReminder { get; set; }

        public string? ReminderEmailSubject { get; set; }

        public string? ReminderEmailBody { get; set; }

        public EmailContentType? ReminderEmailContentType { get; set; }

        public string? ReminderSmsBody { get; set; }

        public required NotificationChannel NotificationChannel { get; set; }

        public NotificationChannel? ReminderNotificationChannel { get; set; }
        
        public string? Language { get; set; }

        public List<Recipient>? CustomRecipients { get; set; }
    }
}
