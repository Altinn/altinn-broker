namespace Altinn.Broker.Core.Models
{
    public class RecipientShipmentStatusOverview
    {
        public string Recipient {get;set;} = string.Empty;
        public RecipientShipmentStatus CurrentRecipientShipmentStatusCode {get;set; }
        public string CurrentrecipientShipmentStatusText {get;set;} = string.Empty;
        public DateTime CurrentRecipientShipmentStatusChanged {get;set;}
    }
}