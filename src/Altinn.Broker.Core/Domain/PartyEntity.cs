namespace Altinn.Broker.Core.Domain;

public class PartyEntity
{
    public DateTimeOffset Created { get; set; }
    public required string OrganizationNumber { get; set; }
    public required string PartyId { get; set; }
}
