using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// API input model for file initialization.
    /// </summary>
    public class ResourceExt
    {
        /// <summary>
        /// The Altinn resource ID
        /// </summary>
        [JsonPropertyName("resourceId")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        public string ResourceId { get; set; } = string.Empty;

        /// <summary>
        /// The max upload size for the resource in bytes
        /// </summary>
        [JsonPropertyName("maxFileTransferSize")]
        [Required]
        public long MaxFileTransferSize { get; set; } = 0;


    }
}
