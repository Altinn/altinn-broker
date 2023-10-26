using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Enums;
using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    public class RecipientFileStatusDetailsExt
    {
        public string Recipient {get;set;} = string.Empty;
        public BrokerFileStatusOverviewExt? File {get;set;}
        public RecipientFileStatusExt CurrentRecipientFileStatusCode {get;set;}
        public string CurrentRecipientFileStatusText {get;set;} = string.Empty;
        public DateTime CurrentRecipientFileStatusChanged {get;set;}
        public List<RecipientFileStatusEventExt>? RecipientFileStatusHistory {get;set;}
    }
}
