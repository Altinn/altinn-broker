using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    public class RecipientFileStatusDetailsExt
    {
        public string Recipient { get; set; } = string.Empty;
        public RecipientFileStatusExt CurrentRecipientFileStatusCode { get; set; }
        public string CurrentRecipientFileStatusText { get; set; } = string.Empty;
        public DateTimeOffset CurrentRecipientFileStatusChanged { get; set; }
    }
}