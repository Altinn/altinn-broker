using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class ShipmentReceipt {
    public Guid ShipmentId { get; set; }
    public Actor Actor { get; set; }
    public ActorShipmentStatus Status { get; set; }
    public DateTimeOffset Date { get; set; }
}
