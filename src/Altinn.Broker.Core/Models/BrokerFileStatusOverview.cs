using Altinn.Broker.Core.Enums;

namespace Altinn.Broker.Core.Models
{
    public class BrokerFileStatusOverview
    {
        public Guid FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string SendersFileReference { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
        public BrokerFileStatus FileStatus { get; set; }
        public string FileStatusText { get; set; } = string.Empty;
        public DateTime FileStatusChanged { get; set; }
    }
}
