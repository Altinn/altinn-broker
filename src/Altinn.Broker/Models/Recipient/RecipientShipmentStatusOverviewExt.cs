using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    public class RecipientShipmentStatusOverviewExt
    {
        public string Recipient { get; set; } = string.Empty;
        public RecipientShipmentStatusExt CurrentRecipientShipmentStatusCode { get; set; }
        public string CurrentRecipientShipmentStatusText { get; set; } = string.Empty;
        public DateTime CurrentRecipientShipmentStatusChanged { get; set; }
    }
}