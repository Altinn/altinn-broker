using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Enums;

namespace Altinn.Broker.Mappers
{
    public static class BrokerCoreMapper
    {

        public static List<RecipientShipmentStatusOverview> MapToRecipientStatusOverview(this List<string> recipients)
        {
            List<RecipientShipmentStatusOverview> recipientOverviews = new();
            foreach(string s in recipients)
            {
                recipientOverviews.Add(new RecipientShipmentStatusOverview()
                {
                    CurrentRecipientShipmentStatusChanged = DateTime.Now,
                    CurrentRecipientShipmentStatusCode = RecipientShipmentStatus.Initialized,
                    CurrentrecipientShipmentStatusText = "Shipment initialized and awaiting file upload.",
                    Recipient = s
                });
            }

            return recipientOverviews;
        }
        public static List<BrokerFileStatusOverview> MapToOverview(this List<BrokerFileInitalize> bfis)
        {
            List<BrokerFileStatusOverview> bfsos = new List<BrokerFileStatusOverview>();
            foreach(var bfi in bfis)
            {
                bfsos.Add(bfi.MapToOverview());                
            }
            
            return bfsos;
        }

        public static BrokerFileStatusOverview MapToOverview(this BrokerFileInitalize bfi)
        {
            BrokerFileStatusOverview bfso = new ()
            {
                Checksum = bfi.Checksum,
                FileId = Guid.NewGuid(),
                FileName = bfi.FileName,
                FileStatus = BrokerFileStatus.AwaitingUpload,
                FileStatusChanged = DateTime.Now,
                FileStatusText = "BrokerFile is initialized and awaiting upload.",
                SendersFileReference = bfi.SendersFileReference
            };
            
            return bfso;
        }

    }
}