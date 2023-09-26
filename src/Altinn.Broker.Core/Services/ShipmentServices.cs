using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Services.Interfaces;

namespace Altinn.Broker.Core.Services
{
    public class ShipmentServices : IShipmentServices
    {
        [AllowNull]
        private static DataStore _dataStore;
        public ShipmentServices()
        {
            _dataStore = DataStore.Instance;
        }

        public async Task<BrokerShipmentMetadata> GetBrokerShipment(Guid shipmentId)
        {
            return await Task.Run(() => _dataStore.BrokerShipStore[shipmentId]);
        }

        public async Task<Guid> SaveBrokerShipment(BrokerShipmentMetadata shipment)
        {
            await Task.Run(() =>_dataStore.BrokerShipStore[shipment.ShipmentId] = shipment);
            return shipment.ShipmentId;
        }

        public async Task UpdateBrokerShipment(BrokerShipmentMetadata shipment)
        {
            await Task.Run(() => _dataStore.BrokerShipStore[shipment.ShipmentId] = shipment);
        }
    }
}