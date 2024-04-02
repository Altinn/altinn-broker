using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Broker.API.Models;

public class ResourceFileTransferTimeToLiveRequest
{
    /// <summary>
    /// The Altinn resource ID
    /// </summary>
    [JsonPropertyName("resourceId")]
    [StringLength(255, MinimumLength = 1)]
    [Required]
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// The time before a file is deleted
    /// </summary>
    [JsonPropertyName("fileTransferTimeToLive")]
    [Required]
    public string FileTransferTimeToLive { get; set; } = "P30D"; // ISO8601 Duration
}
