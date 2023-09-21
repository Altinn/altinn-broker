using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IShipmentRepository
{
    void AddShipment(Shipment shipment);
    List<Shipment> GetAllShipments();
    Shipment? GetShipment(Guid shipmentId);
}