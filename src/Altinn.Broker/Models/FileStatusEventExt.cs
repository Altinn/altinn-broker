using Altinn.Broker.Core.Enums;
using Altinn.Broker.Enums;

namespace Altinn.Broker.Core.Models
{
    public class FileStatusEventExt
    {
        public FileStatusExt FileStatus { get; set; }
        public string FileStatusText { get; set; } = string.Empty;
        public DateTimeOffset FileStatusChanged { get; set; }
    }
}