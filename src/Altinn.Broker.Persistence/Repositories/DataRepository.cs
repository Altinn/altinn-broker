using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Services.Interfaces;

namespace Altinn.Broker.Persistence.Repositories
{
    public class DataStore : IDataService
    {
        private static DataRepo Repository;
        public DataStore()
        {
            Repository = DataRepo.Instance;
        }

        public void SaveBrokerShipmentStatusOverview(BrokerShipmentStatusOverview overview)
        {
            if(overview.ShipmentId == Guid.Empty)
            {
                overview.ShipmentId = Guid.NewGuid();
                foreach(var file in overview.FileList)
                {
                    if(file.FileId == Guid.Empty)
                    {
                        file.FileId = Guid.NewGuid();
                    }
                }
            }
            
            Repository.BrokerShipOverviews[overview.ShipmentId] = overview;
        }

        public BrokerShipmentStatusOverview GetBrokerShipmentStatusOverview(Guid shipmentId)
        {
            return Repository.BrokerShipOverviews[shipmentId] ;
        }

        public void SaveBrokerShipmentMetadata(BrokerShipmentMetadata metadata)
        {
            Repository.BrokerShipStore[metadata.ShipmentId] = metadata;
        }

        public BrokerShipmentMetadata GetBrokerShipmentMetadata(Guid shipmentId)
        {
            return Repository.BrokerShipStore[shipmentId] ;
        }
    }
    public sealed class DataRepo
    {
        public Dictionary<Guid, BrokerShipmentMetadata> BrokerShipStore = new();
        public Dictionary<Guid, BrokerShipmentStatusOverview> BrokerShipOverviews = new();
        private DataRepo(){}
        private static DataRepo? instance = null;
        public static DataRepo Instance {
            get 
            {
                instance ??= new DataRepo();
                return instance;
            }
        }
    }
}