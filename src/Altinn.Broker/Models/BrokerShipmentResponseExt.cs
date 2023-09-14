using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// Entity containing Broker Service Instance metadata
    /// This describes the initiation of a Broker Service and is used in conjunction with a file sender uploading a file.
    /// </summary>
    public class BrokerShipmentResponseExt
    {
        /// <summary>
        /// Gets or sets external service code given when external parties are retrieving service information
        /// </summary>
        [JsonPropertyName("serviceCode")]
        public string ServiceCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets external service edition code when external parties are retrieving service information
        /// </summary>
        [JsonPropertyName("serviceEditionCode")]
        public int ServiceEditionCode { get; set; } = 0;

        /// <summary>
        /// Gets or sets the Receipt text.
        /// </summary>
        [JsonPropertyName("recipients")]
        public List<string> Recipients { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the SendersReference.
        /// </summary>
        [JsonPropertyName("sendersReference")]
        public string SendersReference { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the properties field.
        /// </summary>
        [JsonPropertyName("properties")]
        [AllowNull]
        public Dictionary<string, string> Properties { get; set; }

        /// <summary>
        /// Gets or sets the SendersReference.
        /// </summary>
        [JsonPropertyName("shipmentId")]
        public Guid ShipmentId { get; set; } 

        /// <summary>
        /// Gets or sets the SendersReference.
        /// </summary>
        [JsonPropertyName("files")]
        [AllowNull]
        public List<BrokerFileMetadataExt> Files { get; set; } 

        /// <summary>
        /// Gets or sets the SendersReference.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status {get;set;} = string.Empty;
    }
}