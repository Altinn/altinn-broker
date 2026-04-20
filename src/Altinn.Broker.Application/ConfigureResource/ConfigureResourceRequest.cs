namespace Altinn.Broker.Application.ConfigureResource;
public class ConfigureResourceRequest
{
    public required string ResourceId { get; set; }
    public long? MaxFileTransferSize { get; set; }
    public string? FileTransferTimeToLive { get; set; }
    public bool? PurgeFileTransferAfterAllRecipientsConfirmed { get; set; } = true;
    public string? PurgeFileTransferGracePeriod { get; set; }
    public bool? UseManifestFileShim { get; set; }
    public string? ExternalServiceCodeLegacy { get; set; }
    public int? ExternalServiceEditionCodeLegacy { get; set; }
    public bool? RequiredParty { get; set; }
}
