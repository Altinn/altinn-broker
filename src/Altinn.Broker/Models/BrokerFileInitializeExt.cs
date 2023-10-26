using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Altinn.Broker.Core.Models;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// Entity containing BrokerFile initialization data.
    /// </summary>
    public class BrokerFileInitalizeExt
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
    }
}