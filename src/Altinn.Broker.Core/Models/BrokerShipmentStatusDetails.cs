using Altinn.Broker.Core.Enums;

namespace Altinn.Broker.Core.Models
{
    public class BrokerShipmentStatusDetailed : BrokerShipmentStatusOverview
    {
        public new List<BrokerFileStatusDetails> FileList { get; set; } = new List<BrokerFileStatusDetails>();
        public List<BrokerShipmentStatusEvent> ShipmentStatusHistory { get; set; } = new List<BrokerShipmentStatusEvent>();
    }
}