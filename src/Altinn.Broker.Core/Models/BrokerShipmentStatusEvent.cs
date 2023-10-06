using Altinn.Broker.Core.Enums;

namespace Altinn.Broker.Core.Models
{
    public class BrokerShipmentStatusEvent
    {
        public BrokerShipmentStatus ShipmentStatus {get;set;}
        public string ShipmentStatusText {get;set;}=string.Empty;
        public DateTime ShipmentStatusChanged{get;set;}
    }
}