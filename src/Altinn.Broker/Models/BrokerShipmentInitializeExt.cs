using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Altinn.Broker.Core.Models;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// Entity containing Broker Service Instance metadata
    /// This describes the initiation of a Broker Service and is used in conjunction with a file sender uploading a file.
    /// </summary>
    public class BrokerShipmentInitializeExt
    {

        /// <summary>
        /// Gets or sets the ResourceId for Broker Service
        /// </summary>
        [JsonPropertyName("brokerResourceId")]
        public Guid BrokerResourceId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the sender of the broker shipment.
        /// </summary>
        [JsonPropertyName("sender")]
        public string Sender { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the recipients of the broker shipment.
        /// </summary>
        [JsonPropertyName("recipients")]
        public List<string> Recipients { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the SendersReference.
        /// </summary>
        [JsonPropertyName("sendersShipmentReference")]
        public string SendersShipmentReference { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the properties field.
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets predefined list of files that will be uploaded to the broker shipment.
        /// </summary>
        [JsonPropertyName("files")]
        public List<BrokerFileInitalizeExt> Files { get; set; } = new List<BrokerFileInitalizeExt>();
    }
}