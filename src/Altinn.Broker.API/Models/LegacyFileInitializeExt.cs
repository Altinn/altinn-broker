using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Broker.Helpers;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// API input model for file initialization.
    /// </summary>
    public class LegacyFileInitalizeExt
    {
        /// <summary>
        /// The filename including extension
        /// </summary>
        [JsonPropertyName("filename")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// The Altinn resource ID
        /// </summary>
        [JsonPropertyName("resourceId")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        public string ResourceId { get; set; } = string.Empty;

        /// <summary>
        /// Used by senders and receivers to identify specific file using external identification methods.
        /// </summary>
        [JsonPropertyName("sendersFileTransferReference")]
        [StringLength(4096, MinimumLength = 1)]
        public string SendersFileTransferReference { get; set; } = string.Empty;

        /// <summary>
        /// The sender organization of the file
        /// </summary>
        [JsonPropertyName("sender")]
        [RegularExpressionAttribute(@"^\d{4}:\d{9}$", ErrorMessage = "Organization numbers should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
        [Required]
        public string Sender { get; set; } = string.Empty;

        /// <summary>
        /// The recipient organizations of the broker file.
        /// </summary>
        [JsonPropertyName("recipients")]
        [ValidateElementsInList(typeof(RegularExpressionAttribute), @"^\d{4}:\d{9}$", ErrorMessage = "Each recipient should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
        [Required]
        [MinLength(1, ErrorMessage = "One or more recipients are required")]
        public List<string> Recipients { get; set; } = new List<string>();

        /// <summary>
        /// User-defined properties related to the file
        /// </summary>
        [JsonPropertyName("propertyList")]
        [MaxLength(10, ErrorMessage = "propertyList can contain at most 10 properties")]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// MD5 checksum for file data.
        /// </summary>
        [JsonPropertyName("checksum")]
        [MD5Checksum]
        public string? Checksum { get; set; } = string.Empty;
    }
}
