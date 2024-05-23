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
}
