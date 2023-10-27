using Altinn.Broker.Core.Models;

namespace Altinn.Broker.Core.Services.Interfaces
{
    public interface IShipmentServices
    {
        Task<BrokerShipmentMetadata> GetBrokerShipment(Guid shipmentId);
        Task<Guid> SaveBrokerShipment(BrokerShipmentMetadata shipment);
        Task UpdateBrokerShipment(BrokerShipmentMetadata shipment);
    }
}
