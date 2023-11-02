using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    public class RecipientFileStatusEventExt
    {
        public string Recipient { get; set; } = string.Empty;
        public RecipientFileStatusExt RecipientFileStatusCode { get; set; }
        public string RecipientFileStatusText { get; set; } = string.Empty;
        public DateTime RecipientFileStatusChanged { get; set; }
    }
}
