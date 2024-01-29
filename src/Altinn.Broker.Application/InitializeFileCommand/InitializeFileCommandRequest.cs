
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.InitializeFileCommand;
public class InitializeFileCommandRequest
{
    public CallerIdentity Token { get; set; }
    public string ResourceId { get; set; }
    public string Filename { get; set; }
    public string SendersFileReference { get; set; }
    public string SenderExternalId { get; set; }
    public List<string> RecipientExternalIds { get; set; }
    public Dictionary<string, string> PropertyList { get; set; }
    public string? Checksum { get; set; }
    public bool IsLegacy { get; set; }
}
