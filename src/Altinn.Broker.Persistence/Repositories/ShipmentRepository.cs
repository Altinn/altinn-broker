using Altinn.Broker.Persistence.Models;

namespace Altinn.Broker.Persistence.Repositories;

public class ShipmentRepository {

    private readonly Dictionary<string, Shipment> _shipments;
    public ShipmentRepository(){
        _shipments = new Dictionary<string, Shipment>();
    }

    public Shipment GetShipment(string shipmentId) => _shipments[shipmentId];
    public List<Shipment> GetAllShipments() => _shipments.Values.ToList();

    public void StoreShipment(string shipmentId, Shipment shipment) => _shipments[shipmentId] = shipment;
}
