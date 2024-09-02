using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.ConfigureResource;
public class ConfigureResourceRequest
{
    public required CallerIdentity Token { get; set; }
    public required string ResourceId { get; set; }
    public long? MaxFileTransferSize { get; set; }
    public string? FileTransferTimeToLive { get; set; }
    public bool? DeleteFileTransferAfterAllRecipientsConfirmed { get; set; } = true;
    public string? DeleteFileTransferGracePeriod { get; set; }
}
