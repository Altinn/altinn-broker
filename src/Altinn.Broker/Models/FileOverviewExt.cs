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
        public string ResourceId { get; set; } = string.Empty;
        public string SendersFileReference { get; set; } = string.Empty;
        public string? Checksum { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public FileStatusExt FileStatus { get; set; }
        public string FileStatusText { get; set; } = string.Empty;
        public DateTimeOffset FileStatusChanged { get; set; }
        public DateTimeOffset Created { get; set; }
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
        public List<RecipientFileStatusDetailsExt> Recipients { get; set; } = new List<RecipientFileStatusDetailsExt>();

        /// <summary>
        /// Up to ten arbitrary key value pairs
        /// </summary>
        [JsonPropertyName("propertyList")]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();
    }
}
