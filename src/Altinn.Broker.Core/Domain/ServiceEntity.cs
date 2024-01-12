namespace Altinn.Broker.Core.Domain;

public class ServiceEntity
{
    public long Id { get; set; }
    public DateTimeOffset Created { get; set; }
    public string OrganizationNumber { get; set; }
    public string ServiceOwnerId { get; set; }
}
