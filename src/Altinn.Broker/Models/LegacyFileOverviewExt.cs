using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// Overview of a broker file for use by the LegacyFileController
    /// </summary>
    public class LegacyFileOverviewExt
    {
        /// <summary>
        /// The filename including extension
        /// </summary>
        [JsonPropertyName("fileId")]        
        public Guid FileId { get; set; }

        /// <summary>
        /// Used by senders and receivers to identify specific file using external identification methods.
        /// </summary>
        [JsonPropertyName("filename")]        
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// The Altinn resource ID for the borker 
        /// </summary>
        [JsonPropertyName("resourceId")]
        public string ResourceId { get; set; } = string.Empty;

        /// <summary>
        /// Used by senders and receivers to identify specific file using external identification methods.
        /// </summary>
        [JsonPropertyName("sendersFileReference")]
        public string SendersFileReference { get; set; } = string.Empty;

        /// <summary>
        /// MD5 checksum for file data.
        /// </summary>
        [JsonPropertyName("checksum")]
        public string? Checksum { get; set; } = string.Empty;

        /// <summary>
        /// File size in bytes
        /// </summary>
        [JsonPropertyName("filesize")]
        public long FileSize { get; set; }

        /// <summary>
        /// Current overall File Status
        /// </summary>
        [JsonPropertyName("fileStatus")]
        public LegacyFileStatusExt FileStatus { get; set; }

        /// <summary>
        /// Current overall File Status Text
        /// </summary>
        [JsonPropertyName("fileStatusText")]
        public string FileStatusText { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp for when the Current File Status was changed
        /// </summary>
        [JsonPropertyName("fileStatusChanged")]
        public DateTimeOffset FileStatusChanged { get; set; }

        /// <summary>
        /// Date/Time in UTC for when the file was created
        /// </summary>
        [JsonPropertyName("created")]
        public DateTimeOffset Created { get; set; }

        /// <summary>
        /// Date/Time in UTC for when the file will expire
        /// </summary>
        [JsonPropertyName("expirationTime")]
        public DateTimeOffset ExpirationTime { get; set; }

        /// <summary>
        /// Sender of the file.
        /// </summary>
        [JsonPropertyName("sender")]
        public string Sender { get; set; } = string.Empty;

        /// <summary>
        /// Rcipients of the file
        /// </summary>
        [JsonPropertyName("recipients")]
        public List<LegacyRecipientFileStatusDetailsExt> Recipients { get; set; } = new List<LegacyRecipientFileStatusDetailsExt>();

        /// <summary>
        /// Up to ten arbitrary key value pairs
        /// </summary>
        [JsonPropertyName("propertyList")]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();
    }
}
