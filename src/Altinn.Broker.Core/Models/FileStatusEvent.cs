using Altinn.Broker.Core.Enums;

namespace Altinn.Broker.Core.Models
{
    public class FileStatusEvent
    {
        public BrokerFileStatus FileStatus { get; set; }
        public string FileStatusText { get; set; } = string.Empty;
        public DateTime FileStatusChanged { get; set; }
    }
}