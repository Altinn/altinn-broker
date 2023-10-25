using System.Diagnostics.CodeAnalysis;

using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;

namespace Altinn.Broker.Core.Services.Interfaces
{    
    public interface IDataService
    {
        void SaveBrokerShipmentStatusOverview(BrokerShipmentStatusOverview overview);
        void SaveBrokerShipmentMetadata(BrokerShipmentMetadata metadata);
        BrokerShipmentStatusOverview GetBrokerShipmentStatusOverview(Guid shipmentId);
        BrokerShipmentMetadata GetBrokerShipmentMetadata(Guid shipmentId);
    }    
}