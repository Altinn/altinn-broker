using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    public class LegacyRecipientFileStatusEventExt
    {
        public string Recipient { get; set; } = string.Empty;
        public LegacyRecipientFileStatusExt RecipientFileStatusCode { get; set; }
        public string RecipientFileStatusText { get; set; } = string.Empty;
        public DateTimeOffset RecipientFileStatusChanged { get; set; }
    }
}
