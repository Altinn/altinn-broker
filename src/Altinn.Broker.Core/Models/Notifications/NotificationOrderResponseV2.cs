namespace Altinn.Broker.Core.Models.Notifications
{
    public class NotificationOrderResponseV2
    {
        public Guid NotificationOrderId { get; set; }

        public NotificationResponseV2 Notification { get; set; } = null!;
    }

    public class NotificationResponseV2
    {
        public List<ReminderResponse> Reminders { get; set; } = new();

        public Guid ShipmentId { get; set; }

        public string SendersReference { get; set; } = null!;
    }

    public class ReminderResponse
    {
        public Guid ShipmentId { get; set; }

        public string SendersReference { get; set; } = null!;
    }
} 