using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    public class RecipientShipmentStatusDetailsExt : RecipientShipmentStatusOverviewExt
    {
        public List<RecipientShipmentStatusEventExt>? RecipientShipmentStatusHistory { get; set; }
    }
}