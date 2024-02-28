
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.InitializeFileTransferCommand;
public class InitializeFileTransferCommandRequest
{
    public CallerIdentity Token { get; set; }
    public string ResourceId { get; set; }
    public string FileName { get; set; }
    public string SendersFileTransferReference { get; set; }
    public string SenderExternalId { get; set; }
    public List<string> RecipientExternalIds { get; set; }
    public Dictionary<string, string> PropertyList { get; set; }
    public string? Checksum { get; set; }
    public bool IsLegacy { get; set; }
}
