using Altinn.Broker.Core.Models;

namespace Altinn.Broker.Core.Helpers
{
    public sealed class DataStore
    {
        public Dictionary<Guid, BrokerShipment> BrokerShipStore = new();
        private DataStore(){}
        private static DataStore? instance = null;
        public static DataStore Instance {
            get 
            {
                instance ??= new DataStore();
                return instance;
            }
        }
    }
}