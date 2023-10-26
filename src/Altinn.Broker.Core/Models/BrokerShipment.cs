
using Altinn.Broker.Core.Enums;

namespace Altinn.Broker.Core.Models
{
    /// <summary>
    /// Entity containing Broker Service Instance metadata
    /// This describes the initiation of a Broker Service and is used in conjunction with a file sender uploading a file.
    /// </summary>
    public class BrokerShipmentMetadata
    {
        public Guid BrokerResourceId { get; set; }
        /// <summary>
        /// Gets or sets external service code given when external parties are retrieving service information
        /// </summary>
        public string ServiceCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets external service edition code when external parties are retrieving service information
        /// </summary>
        public int ServiceEditionCode { get; set; } = 0;

        /// <summary>
        /// Gets or sets the Receipt text.
        /// </summary>
        public List<string> Recipients { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the SendersReference.
        /// </summary>
        public string SendersReference { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the properties field.
        /// </summary>
        public Dictionary<string, string>? Properties { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets files related to shipment.
        /// </summary>
        public List<BrokerFileMetadata> FileList { get; set; } = new List<BrokerFileMetadata>();

        public BrokerShipmentStatus Status { get; set; } = BrokerShipmentStatus.Initialized;
        public Guid ShipmentId { get; set; }
    }
}