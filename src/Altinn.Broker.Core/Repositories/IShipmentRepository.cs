using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IShipmentRepository
{
    Task AddShipmentAsync(Shipment shipment);
    Task<List<Shipment>> GetAllShipmentsAsync();
    Task<Shipment?> GetShipmentAsync(Guid shipmentId);
}
