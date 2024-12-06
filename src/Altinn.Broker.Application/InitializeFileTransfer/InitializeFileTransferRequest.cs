
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.InitializeFileTransfer;
public class InitializeFileTransferRequest
{
    public required string ResourceId { get; set; }
    public required string FileName { get; set; }
    public required string SendersFileTransferReference { get; set; }
    public required string SenderExternalId { get; set; }
    public required List<string> RecipientExternalIds { get; set; }
    public required Dictionary<string, string> PropertyList { get; set; }
    public string? Checksum { get; set; }
    public bool IsLegacy { get; set; }
    public bool DisableVirusScan { get; set; }
}
