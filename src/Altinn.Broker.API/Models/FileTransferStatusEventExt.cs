using Altinn.Broker.Enums;

namespace Altinn.Broker.Core.Models
{
    public class FileTransferStatusEventExt
    {
        public FileTransferStatusExt FileTransferStatus { get; set; }
        public string FileTransferStatusText { get; set; } = string.Empty;
        public DateTimeOffset FileTransferStatusChanged { get; set; }
    }
}
