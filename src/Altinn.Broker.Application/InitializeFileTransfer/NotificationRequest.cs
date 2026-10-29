using Altinn.Broker.Core.Models.Notifications;

namespace Altinn.Broker.Application.InitializeFileTransfer
{
    public class NotificationRequest
    {
        public bool SendReminder { get; set; }

        public List<Recipient>? CustomRecipients { get; set; }
    }
}
