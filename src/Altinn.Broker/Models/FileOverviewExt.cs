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
        public string FileName { get; set; } = string.Empty;
        public string SendersFileReference { get; set; } = string.Empty;
        public string? Checksum { get; set; } = string.Empty;
        public FileStatusExt FileStatus { get; set; }
        public string FileStatusText { get; set; } = string.Empty;
        public DateTimeOffset FileStatusChanged { get; set; }

        /// <summary>
        /// Sender of the file.
        /// </summary>
        [JsonPropertyName("sender")]
        public string Sender { get; set; } = string.Empty;

        /// <summary>
        /// Rcipients of the file
        /// </summary>
        [JsonPropertyName("recipients")]
        public List<RecipientFileStatusDetailsExt> Recipients { get; set; } = new List<RecipientFileStatusDetailsExt>();

        /// <summary>
        /// Up to ten arbitrary key value pairs
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();
    }
}