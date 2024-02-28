using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    public class RecipientFileTransferStatusDetailsExt
    {
        public string Recipient { get; set; } = string.Empty;
        public RecipientFileTransferStatusExt CurrentRecipientFileTransferStatusCode { get; set; }
        public string CurrentRecipientFileTransferStatusText { get; set; } = string.Empty;
        public DateTimeOffset CurrentRecipientFileTransferStatusChanged { get; set; }
    }
}
