using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Broker.Helpers;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// API input model for file initialization.
    /// </summary>
    public class FileInitalizeExt
    {
        /// <summary>
        /// The filename including extension
        /// </summary>
        [JsonPropertyName("filename")]
        [Length(1,255)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Used by senders and receivers to identify specific file using external identification methods.
        /// </summary>
        [JsonPropertyName("sendersFileReference")]
        [Length(1, 4096)]
        public string SendersFileReference { get; set; } = string.Empty;

        /// <summary>
        /// The sender organization of the file
        /// </summary>
        [JsonPropertyName("sender")]
        [RegularExpressionAttribute(@"^\d{4}:\d{9}$", ErrorMessage = "Organization numbers should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
        public string Sender { get; set; } = string.Empty;

        /// <summary>
        /// The recipient organizations of the broker file.
        /// </summary>
        [JsonPropertyName("recipients")]
        [ValidateElementsInList(typeof(RegularExpressionAttribute), @"^\d{4}:\d{9}$", ErrorMessage = "Each recipient should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
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
        [StringLength(32, MinimumLength = 32, ErrorMessage = "The checksum, if used, must be a MD5 hash with a length of 32 characters")]
        public string? Checksum { get; set; } = string.Empty;
    }
}
