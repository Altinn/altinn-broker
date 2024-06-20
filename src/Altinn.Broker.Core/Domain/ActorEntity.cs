namespace Altinn.Broker.Core.Domain;

public class ActorEntity
{
    public long ActorId { get; set; }
    public required string ActorExternalId { get; set; }
}
