
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

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
    public ChecksumAlgorithm ChecksumAlgorithm { get; set; } = ChecksumAlgorithm.MD5;
    public bool DisableVirusScan { get; set; }
    public NotificationRequest? Notification { get; set;}
}
