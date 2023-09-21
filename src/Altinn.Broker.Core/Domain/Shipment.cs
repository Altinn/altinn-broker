using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class Shipment
{
    public Guid ShipmentId { get; set; }
    public string ExternalShipmentReference { get; set; }
    public long UploaderActorId { get; set; }
    public DateTimeOffset Initiated { get; set; }
    public ShipmentStatus ShipmentStatus { get; set; } // Joined in
    public List<ShipmentReceipt> Receipts { get; set; } // Joined in
}
