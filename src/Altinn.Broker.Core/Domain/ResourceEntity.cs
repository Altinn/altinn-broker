namespace Altinn.Broker.Core.Domain;

public class ResourceEntity
{
    public string Id { get; set; }
    public DateTimeOffset? Created { get; set; }
    public string? OrganizationNumber { get; set; }
    public string ServiceOwnerId { get; set; }
    public long? MaxFileTransferSize { get; set; }
    public TimeSpan? FileRetentionTime { get; set; }
}
