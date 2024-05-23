using Altinn.Broker.Enums;

namespace Altinn.Broker.Models;

public class LegacyRecipientFileStatusDetailsExt
{
    public string Recipient { get; set; } = string.Empty;
    public LegacyRecipientFileStatusExt CurrentRecipientFileStatusCode { get; set; }
    public string CurrentRecipientFileStatusText { get; set; } = string.Empty;
    public DateTimeOffset CurrentRecipientFileStatusChanged { get; set; }
}
