using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Enums;
using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    public class BrokerShipmentStatusEventExt
    {
        public BrokerShipmentStatusExt ShipmentStatus {get;set;}
        public string ShipmentStatusText {get;set;}=string.Empty;
        public DateTime ShipmentStatusChanged{get;set;}
    }
}
