namespace Altinn.Broker.Application.ConfigureResource;
public class ConfigureResourceRequest
{
    public required string ResourceId { get; set; }
    /// <summary>Organization the ID-porten end user acts on behalf of when configuring the resource.</summary>
    public string? OnBehalfOf { get; set; }
    public long? MaxFileTransferSize { get; set; }
    public string? FileTransferTimeToLive { get; set; }
    public bool? PurgeFileTransferAfterAllRecipientsConfirmed { get; set; } = true;
    public string? PurgeFileTransferGracePeriod { get; set; }
    public bool? UseManifestFileShim { get; set; }
    public string? ExternalServiceCodeLegacy { get; set; }
    public int? ExternalServiceEditionCodeLegacy { get; set; }
    public string? RequiredParty { get; set; }
}
