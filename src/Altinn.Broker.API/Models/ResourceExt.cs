using System.Text.Json.Serialization;

namespace Altinn.Broker.Models;

/// <summary>
/// API input model for file initialization.
/// </summary>
public class ResourceExt
{

    /// <summary>
    /// The max upload size for the resource in bytes
    /// </summary>
    [JsonPropertyName("maxFileTransferSize")]
    public long? MaxFileTransferSize { get; set; }

    /// <summary>
    /// The time before a file transfer expires (ISO8601 Duration format)
    /// </summary>
    [JsonPropertyName("fileTransferTimeToLive")]
    public string? FileTransferTimeToLive { get; set; }

    /// <summary>
    /// If the file transfer should be deleted after all recipients have confirmed
    /// </summary>
    [JsonPropertyName("purgeFileTransferAfterAllRecipientsConfirmed")]
    public bool? PurgeFileTransferAfterAllRecipientsConfirmed { get; set; }

    /// <summary>
    /// The grace period before a file transfer is deleted after all recipients have confirmed (ISO8601 Duration format)
    /// </summary>
    [JsonPropertyName("purgeFileTransferGracePeriod")]
    public string? PurgeFileTransferGracePeriod { get; set; }

    /// <summary>
    /// If the manifest file shim should be used in the transition solution where manifest files are added to downloaded files
    /// </summary>
    [JsonPropertyName("useManifestFileShim")]
    public bool? UseManifestFileShim { get; set; }

    [JsonPropertyName("externalServiceCodeLegacy")]
    public string? ExternalServiceCodeLegacy { get; set; }

    [JsonPropertyName("externalServiceEditionCodeLegacy")]
    public string? ExternalServiceEditionCodeLegacy { get; set; }
}
