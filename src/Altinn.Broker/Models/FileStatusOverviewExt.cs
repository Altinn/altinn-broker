using System.Text.Json.Serialization;

using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// Entity containing Broker Service Instance metadata
    /// This describes the initiation of a Broker Service and is used in conjunction with a file sender uploading a file.
    /// </summary>
    public class FileStatusOverviewExt
    {
        public Guid FileId {get;set;}
        public string FileName {get;set;}=string.Empty;
        public string SendersFileReference {get;set;}=string.Empty;
        public string Checksum{get;set;}=string.Empty;
        public FileStatusExt FileStatus {get;set;}
        public string FileStatusText {get;set;} = string.Empty;
        public DateTime FileStatusChanged{get;set;}
        
        /// <summary>
        /// Gets or sets the ResourceId for broker service
        /// </summary>
        [JsonPropertyName("brokerResourceId")]
        public Guid BrokerResourceId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the sender of the file.
        /// </summary>
        [JsonPropertyName("sender")]
        public string Sender { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the recipients of the file
        /// </summary>
        [JsonPropertyName("recipients")]
        public List<string> Recipients { get; set; } = new List<string>();
        
        /// <summary>
        /// Gets or sets the properties field.
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}