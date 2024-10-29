using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.ConfigureResource;
public class ConfigureResourceRequest
{
    public required CallerIdentity Token { get; set; }
    public required string ResourceId { get; set; }
    public long? MaxFileTransferSize { get; set; }
    public string? FileTransferTimeToLive { get; set; }
    public bool? PurgeFileTransferAfterAllRecipientsConfirmed { get; set; } = true;
    public string? PurgeFileTransferGracePeriod { get; set; }
    public bool? UseManifestFileShim { get; set; }
}
