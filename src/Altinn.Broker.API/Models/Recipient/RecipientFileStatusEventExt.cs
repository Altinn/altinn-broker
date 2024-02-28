using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    public class RecipientFileTransferStatusEventExt
    {
        public string Recipient { get; set; } = string.Empty;
        public RecipientFileTransferStatusExt RecipientFileTransferStatusCode { get; set; }
        public string RecipientFileTransferStatusText { get; set; } = string.Empty;
        public DateTimeOffset RecipientFileTransferStatusChanged { get; set; }
    }
}
