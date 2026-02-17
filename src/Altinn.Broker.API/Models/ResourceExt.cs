using System.Text.Json.Serialization;

using Altinn.Broker.Helpers;

namespace Altinn.Broker.Models;

/// <summary>
/// A model representing the extended resource properties unique for the broker service.
/// </summary>
[ValidateUseManifestFileShim]

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

    /// <summary>
    /// The external service code used in Altinn 2 for the broker service
    /// </summary>
    [JsonPropertyName("externalServiceCodeLegacy")]
    public string? ExternalServiceCodeLegacy { get; set; }

    /// <summary>
    /// The external service edition code used in Altinn 2 for the broker service
    /// </summary>
    [JsonPropertyName("externalServiceEditionCodeLegacy")]
    public int? ExternalServiceEditionCodeLegacy { get; set; }

    /// <summary>
    /// If the resource requires the service owner party to be subject of the file transfer.
    /// </summary>>
    [JsonPropertyName("requiredParty")]
    public bool? RequiredParty { get; set; }
}
