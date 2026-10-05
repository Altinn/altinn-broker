using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Models.Notifications;

namespace Altinn.Broker.Application.InitializeFileTransfer
{
    public class NotificationRequest
    {
        public required NotificationChannel NotificationChannel { get; set; }

        public bool SendReminder { get; set; }

        public List<Recipient>? CustomRecipients { get; set; }
    }
}
