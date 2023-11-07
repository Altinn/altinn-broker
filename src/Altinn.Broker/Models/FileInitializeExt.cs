using System.Text.Json.Serialization;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// Entity containing BrokerFile initialization data.
    /// </summary>
    public class FileInitalizeExt
    {
        /// <summary>
        /// Gets or sets the original filename
        /// </summary>
        [JsonPropertyName("filename")]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the senders file reference. Used by senders and receivers to identify specific file using external identification methods.
        /// </summary>
        [JsonPropertyName("sendersFileReference")]
        public string SendersFileReference { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets checksum for file data.
        /// </summary>
        [JsonPropertyName("checksum")]
        public string Checksum { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sender of the broker file.
        /// </summary>
        [JsonPropertyName("sender")]
        public string Sender { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the recipients of the broker file.
        /// </summary>
        [JsonPropertyName("recipients")]
        public List<string> Recipients { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the properties field.
        /// </summary>
        [JsonPropertyName("propertyList")]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();
    }
}