using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers
{
    public static class BrokerShipmentMapper
    {
        /// <summary>
        /// Maps a <see cref="InitiateBrokerShipmentRequestExt"/> to a <see cref="BrokerShipmentMetadata"/>
        /// </summary>
        public static BrokerShipmentMetadata MapToBrokerShipment(this InitiateBrokerShipmentRequestExt extRequest)
        {
            BrokerShipmentMetadata shipment = new()
            {
                ServiceCode = extRequest.ServiceCode,
                ServiceEditionCode = extRequest.ServiceEditionCode,
                SendersReference = extRequest.SendersReference,
                Recipients = extRequest.Recipients,
                Properties = extRequest.Properties
            };

            return shipment;
        }

        public static BrokerShipmentResponseExt MapToBrokerShipmentExtResponse(this BrokerShipmentMetadata shipment)
        {
            BrokerShipmentResponseExt response = new()
            {
                ServiceCode = shipment.ServiceCode,
                ServiceEditionCode = shipment.ServiceEditionCode,
                SendersReference = shipment.SendersReference,
                Recipients = shipment.Recipients,
                Properties = shipment.Properties,
                ShipmentId = shipment.ShipmentId,
                Status = (BrokerShipmentStatusExt)shipment.Status
            };

            List<BrokerFileMetadataExt> metaDataExt = new List<BrokerFileMetadataExt>();
            shipment.FileList?.ForEach(f => metaDataExt.Add(f.MapToBrokerFileMetadataExt()));
            if(metaDataExt.Count > 0)
            {
                response.Files = metaDataExt;
            }
            
            return response;
        }

        public static BrokerFileMetadataExt MapToBrokerFileMetadataExt(this BrokerFileMetadata brokerFile)
        {
            BrokerFileMetadataExt metadataExt = new BrokerFileMetadataExt()
            {
                FileId = brokerFile.FileId,
                FileName = brokerFile.FileName,
                FileStatus = brokerFile.FileStatus,
                SendersFileReference = brokerFile.SendersFileReference,
                ShipmentId = brokerFile.ShipmentId
            };
            return metadataExt;
        }
    }
}