using Altinn.Broker.Core.Enums;

namespace Altinn.Broker.Core.Models
{
    public class BrokerShipmentStatusOverview
    {
        public Guid BrokerResourceId { get; set; }
        public Guid ShipmentId { get; set; }
        public string Sender { get; set; } = string.Empty;
        public string SendersShipmentReference { get; set; } = string.Empty;
        public List<RecipientShipmentStatusOverview> RecipientStatusList { get; set; } = new List<RecipientShipmentStatusOverview>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public DateTime ShipmentInitialized { get; set; }
        public BrokerShipmentStatus CurrentShipmentStatus { get; set; }
        public string CurrentShipmentStatusText { get; set; } = string.Empty;
        public DateTime CurrentShipmentStatusChanged { get; set; }
        public List<BrokerFileStatusOverview> FileList { get; set; } = new List<BrokerFileStatusOverview>();
    }
}