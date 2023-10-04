using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

using Npgsql.Replication;

namespace Altinn.Broker.Mappers
{
    public static class BrokerShipmentMapper
    {
        public static BrokerShipmentStatusDetailsExt MapToDetailsExternal(this BrokerShipmentStatusDetails ibsd)
        {
            BrokerShipmentStatusDetailsExt detailsExt = new()
            {
                BrokerResourceId = ibsd.BrokerResourceId,
                CurrentShipmentStatus = ibsd.CurrentShipmentStatus,
                CurrentShipmentStatusChanged = ibsd.CurrentShipmentStatusChanged,
                CurrentShipmentStatusText = ibsd.CurrentShipmentStatusText,
                FileList = new List<BrokerFileStatusDetailsExt>(),
                Metadata = ibsd.Metadata,
                RecipientStatusList = ibsd.RecipientStatusList,
                Sender = ibsd.Sender,
                SendersShipmentReference = ibsd.SendersShipmentReference,
                ShipmentId = ibsd.ShipmentId,
                ShipmentInitialized = ibsd.ShipmentInitialized,
                ShipmentStatusHistory = new List<BrokerShipmentStatusEventExt>()
            };

            foreach(var i in ibsd.FileList)
            {
                detailsExt.FileList.Add(i.MapToDetailsExternal());
            }

            foreach(var ss in ibsd.ShipmentStatusHistory)
            {
                detailsExt.ShipmentStatusHistory.Add(ss.MapToExternal());
            }

            return detailsExt;
        }

        public static BrokerShipmentStatusEventExt MapToExternal(this BrokerShipmentStatusEvent ibse)
        {
            BrokerShipmentStatusEventExt ext = (BrokerShipmentStatusEventExt)ibse;
            return ext;
        }

        public static BrokerFileStatusDetailsExt MapToDetailsExternal(this BrokerFileStatusDetails ibfd)
        {
            BrokerFileStatusDetailsExt detailsExt = new()
            {
                Checksum = ibfd.Checksum,
                FileId = ibfd.FileId,
                FileName = ibfd.FileName,
                FileStatus =  ibfd.FileStatus,
                FileStatusChanged = ibfd.FileStatusChanged,
                FileStatusHistory = new List<FileStatusEventExt>(),
                FileStatusText = ibfd.FileStatusText,
                SendersFileReference = ibfd.SendersFileReference
            };

            foreach(var fs in ibfd.FileStatusHistory)
            {
                detailsExt.FileStatusHistory.Add(fs.MapToExternal());
            }

            return detailsExt;
        }

        public static FileStatusEventExt MapToExternal(this FileStatusEvent ifse)
        {
            FileStatusEventExt ext = new()
            {
                FileStatus = ifse.FileStatus,
                FileStatusChanged = ifse.FileStatusChanged,
                FileStatusText = ifse.FileStatusText
            };

            return ext;
        }

        public static BrokerShipmentStatusOverviewExt MapToOverviewExternal(this BrokerShipmentStatusOverview ibsso)
        {
            BrokerShipmentStatusOverviewExt ebsso = new()
            {
                BrokerResourceId = ibsso.BrokerResourceId,
                Metadata = ibsso.Metadata,
                RecipientStatusList = ibsso.RecipientStatusList,
                Sender = ibsso.Sender,
                SendersShipmentReference = ibsso.SendersShipmentReference,
                CurrentShipmentStatus = ibsso.CurrentShipmentStatus,
                ShipmentId = ibsso.ShipmentId,
                CurrentShipmentStatusChanged = ibsso.CurrentShipmentStatusChanged,
                CurrentShipmentStatusText = ibsso.CurrentShipmentStatusText,
                ShipmentInitialized = ibsso.ShipmentInitialized,
                FileList = new List<BrokerFileStatusOverviewExt>()
            };

            foreach(BrokerFileStatusOverview bfso in ibsso.FileList)
            {
                ebsso.FileList.Add(bfso.MapToOverviewExternal());
            }

            return ebsso;
        }

        public static BrokerFileStatusOverviewExt MapToOverviewExternal(this BrokerFileStatusOverview ibfso)
        {
            BrokerFileStatusOverviewExt ebfso = new ()
            {
                Checksum = ibfso.Checksum,
                FileId = ibfso.FileId,
                FileName = ibfso.FileName,
                FileStatus = ibfso.FileStatus,
                FileStatusChanged = ibfso.FileStatusChanged,
                FileStatusText = ibfso.FileStatusText,
                SendersFileReference = ibfso.SendersFileReference
            };

            return ebfso;
        }

        public static BrokerShipmentInitialize MapToBrokerShipmentInitialize(this BrokerShipmentInitializeExt extRequest)
        {
            BrokerShipmentInitialize bsi = new BrokerShipmentInitialize()
            {
                 BrokerResourceId = extRequest.BrokerResourceId,
                 Files = extRequest.Files.ToList<BrokerFileInitalize>(),
                 Metadata = extRequest.Metadata,
                 Recipients = extRequest.Recipients,
                 Sender = extRequest.Sender,
                 SendersShipmentReference = extRequest.SendersShipmentReference
            };

            return bsi;
        }
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