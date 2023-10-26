using System.Linq;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

using Npgsql.Replication;

namespace Altinn.Broker.Mappers
{
    public static class BrokerShipmentMapper
    {
        public static List<BrokerShipmentStatusDetailedExt> MapToExternal(this List<BrokerShipmentStatusDetailed> i)
        {
            return i.Select(status => status.MapToExternal()).ToList();
        }
        public static BrokerShipmentStatusDetailedExt MapToExternal(this BrokerShipmentStatusDetailed ibsd)
        {
            return new BrokerShipmentStatusDetailedExt()
            {
                BrokerResourceId = ibsd.BrokerResourceId,
                CurrentShipmentStatus = ibsd.CurrentShipmentStatus,
                CurrentShipmentStatusChanged = ibsd.CurrentShipmentStatusChanged,
                CurrentShipmentStatusText = ibsd.CurrentShipmentStatusText,
                FileList = ibsd.FileList.MapToExternal(),
                Metadata = ibsd.Metadata,
                RecipientStatusList = ibsd.RecipientStatusList,
                Sender = ibsd.Sender,
                SendersShipmentReference = ibsd.SendersShipmentReference,
                ShipmentId = ibsd.ShipmentId,
                ShipmentInitialized = ibsd.ShipmentInitialized,
                ShipmentStatusHistory = ibsd.ShipmentStatusHistory.MapToExternal()
            };
        }
        public static List<BrokerShipmentStatusEventExt> MapToExternal(this List<BrokerShipmentStatusEvent> i)
        {
            return i.Select(e => e.MapToExternal()).ToList();
        }
        public static BrokerShipmentStatusEventExt MapToExternal(this BrokerShipmentStatusEvent ibse)
        {
            BrokerShipmentStatusEventExt ext = new()
            {
                ShipmentStatusChanged = ibse.ShipmentStatusChanged,
                ShipmentStatusText = ibse.ShipmentStatusText,
                ShipmentStatus = (BrokerShipmentStatusExt)ibse.ShipmentStatus
            };

            return ext;
        }

        public static List<BrokerShipmentStatusOverviewExt> MapToExternal(this List<BrokerShipmentStatusOverview> ibssos)
        {
            return ibssos.Select(ibsso => ibsso.MapToExternal()).ToList();
        }

        public static BrokerShipmentStatusOverviewExt MapToExternal(this BrokerShipmentStatusOverview ibsso)
        {
            return new BrokerShipmentStatusOverviewExt()
            {
                BrokerResourceId = ibsso.BrokerResourceId,
                Metadata = ibsso.Metadata,
                RecipientStatusList = ibsso.RecipientStatusList,
                Sender = ibsso.Sender,
                SendersShipmentReference = ibsso.SendersShipmentReference,
                CurrentShipmentStatus = (BrokerShipmentStatusExt)ibsso.CurrentShipmentStatus,
                ShipmentId = ibsso.ShipmentId,
                CurrentShipmentStatusChanged = ibsso.CurrentShipmentStatusChanged,
                CurrentShipmentStatusText = ibsso.CurrentShipmentStatusText,
                ShipmentInitialized = ibsso.ShipmentInitialized,
                FileList = ibsso.FileList.Select(f => f.MapToExternal()).ToList()
            };
        }

        public static BrokerShipmentInitialize MapToInternal(this BrokerShipmentInitializeExt extRequest)
        {
            BrokerShipmentInitialize bsi = new BrokerShipmentInitialize()
            {
                BrokerResourceId = extRequest.BrokerResourceId,
                Files = extRequest.Files.MapToInternal(),
                Metadata = extRequest.Metadata,
                Recipients = extRequest.Recipients,
                Sender = extRequest.Sender,
                SendersShipmentReference = extRequest.SendersShipmentReference
            };

            return bsi;
        }

    }
}