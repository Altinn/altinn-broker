using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// Entity containing Broker Service Shipment metadata
    /// This describes the initiation of a Broker Service and is used in conjunction with a file sender uploading a file.
    /// </summary>
    public class BrokerShipmentStatusOverviewExt
    {
        public Guid BrokerResourceId { get; set; }
        public Guid ShipmentId { get; set; }
        public string Sender { get; set; } = string.Empty;
        public string SendersShipmentReference { get; set; } = string.Empty;
        public List<RecipientShipmentStatusOverview> RecipientStatusList { get; set; } = new List<RecipientShipmentStatusOverview>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public DateTime ShipmentInitialized { get; set; }
        public BrokerShipmentStatusExt CurrentShipmentStatus { get; set; }
        public string CurrentShipmentStatusText { get; set; } = string.Empty;
        public DateTime CurrentShipmentStatusChanged { get; set; }
        public List<BrokerFileStatusOverviewExt> FileList { get; set; } = new List<BrokerFileStatusOverviewExt>();
    }
}