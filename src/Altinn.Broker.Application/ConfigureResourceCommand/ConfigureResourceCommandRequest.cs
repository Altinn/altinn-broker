using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.ConfigureResourceCommand;
public class ConfigureResourceCommandRequest
{
    public required CallerIdentity Token { get; set; }
    public required string ResourceId { get; set; }
    public long? MaxFileTransferSize { get; set; }
    public string? FileTransferTimeToLive { get; set; }
}
