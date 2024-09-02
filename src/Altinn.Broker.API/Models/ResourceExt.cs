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
    [JsonPropertyName("deleteFileTransferAfterAllRecipientsConfirmed")]
    public bool? DeleteFileTransferAfterAllRecipientsConfirmed { get; set; }

    /// <summary>
    /// The grace period before a file transfer is deleted after all recipients have confirmed (ISO8601 Duration format)
    /// </summary>
    [JsonPropertyName("deleteFileTransferGracePeriod")]
    public string? DeleteFileTransferGracePeriod { get; set; }
}
