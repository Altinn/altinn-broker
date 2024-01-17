namespace Altinn.Broker.Core.Domain;

public class ResourceEntity
{
    public string Id { get; set; }
    public DateTimeOffset Created { get; set; }
    public string OrganizationNumber { get; set; }
    public string ResourceOwnerId { get; set; }
}
