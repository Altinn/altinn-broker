using Altinn.Broker.Core.Models;

namespace Altinn.Broker.Core.Services.Interfaces
{    public interface IShipmentService
    {
        Task<BrokerShipment> GetBrokerShipment(Guid shipmentId);
        Task<Guid> SaveBrokerShipment(BrokerShipment shipment);
        Task UpdateBrokerShipment(BrokerShipment shipment);
    }    
}