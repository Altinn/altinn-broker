using System.Text.Json.Serialization;

using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// Overview of a broker file
    /// </summary>
    public class FileOverviewExt
    {
        public Guid FileId { get; set; }

        /// <summary>
        /// The Altinn resource ID for the broker service 
        /// </summary>
        [JsonPropertyName("resourceId")]
        public string ResourceId { get; set; } = string.Empty;

        /// <summary>
        /// The filename including extension
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Used by senders and receivers to identify specific file using external identification methods.
        /// </summary>
        public string SendersFileReference { get; set; } = string.Empty;

        /// <summary>
        /// MD5 checksum for file data.
        /// </summary>
        public string? Checksum { get; set; } = string.Empty;

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Current file status
        /// </summary>
        public FileStatusExt FileStatus { get; set; }

        /// <summary>
        /// Current file status text description
        /// </summary>
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
        /// Recipients of the file
        /// </summary>
        [JsonPropertyName("recipients")]
        public List<RecipientFileStatusDetailsExt> Recipients { get; set; } = new List<RecipientFileStatusDetailsExt>();

        /// <summary>
        /// Up to ten arbitrary key value pairs
        /// </summary>
        [JsonPropertyName("propertyList")]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();
    }
}
