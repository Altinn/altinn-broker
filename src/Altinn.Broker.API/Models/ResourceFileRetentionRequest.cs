using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Broker.API.Models;

public class ResourceFileRetentionRequest
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
    [JsonPropertyName("fileRetentionTime")]
    [Required]
    public string FileRetentionTime { get; set; } = "P30D"; // ISO8601 Duration
}
