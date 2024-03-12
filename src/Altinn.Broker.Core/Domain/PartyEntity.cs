namespace Altinn.Broker.Core.Domain;

public class PartyEntity
{
    public DateTimeOffset Created { get; set; }
    public string OrganizationNumber { get; set; }
    public string PartyId { get; set; }
}
