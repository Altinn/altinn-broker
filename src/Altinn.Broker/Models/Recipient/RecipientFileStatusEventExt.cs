using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    public class RecipientFileStatusEventExt
    {
        public string Recipient { get; set; } = string.Empty;
        public Guid FileId { get; set; }
        public RecipientFileStatusExt RecipientFileStatusCode { get; set; }
        public string RecipientFileStatusText { get; set; } = string.Empty;
        public DateTime RecipientFileStatusChanged { get; set; }
    }
}