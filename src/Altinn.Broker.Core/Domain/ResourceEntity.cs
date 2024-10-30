namespace Altinn.Broker.Core.Domain;

public class ResourceEntity
{
    public required string Id { get; set; }
    public DateTimeOffset? Created { get; set; }
    public string? OrganizationNumber { get; set; }
    public required string ServiceOwnerId { get; set; }
    public long? MaxFileTransferSize { get; set; }
    public TimeSpan? FileTransferTimeToLive { get; set; }
    public bool PurgeFileTransferAfterAllRecipientsConfirmed { get; set; } = true;
    public TimeSpan? PurgeFileTransferGracePeriod { get; set; }
    public bool? UseManifestFileShim { get; set; }
}
