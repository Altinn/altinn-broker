using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    public class RecipientShipmentStatusEventExt
    {
        public string Recipient { get; set; } = string.Empty;
        public RecipientShipmentStatusExt RecipientShipmentStatusCode { get; set; }
        public string RecipientShipmentStatusText { get; set; } = string.Empty;
        public DateTime RecipientShipmentStatusChanged { get; set; }
    }
}
