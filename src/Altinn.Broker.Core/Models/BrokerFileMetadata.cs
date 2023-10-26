using Altinn.Broker.Core.Enums;

namespace Altinn.Broker.Core.Models
{
    public class BrokerFileMetadata
    {
        public string FileName { get; set; } = string.Empty;
        public string SendersFileReference { get; set; } = string.Empty;
        public Guid FileId { get; set; }
        public Guid ShipmentId { get; set; }
        public BrokerFileStatus FileStatus { get; set; } = BrokerFileStatus.Initialized;
        public string GetId() => FileId.ToString();
    }
}