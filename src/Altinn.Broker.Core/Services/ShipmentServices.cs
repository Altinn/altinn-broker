using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Services.Interfaces;

namespace Altinn.Broker.Core.Services
{
    public class ShipmentService : IShipmentService
    {
        [AllowNull]
        private static DataStore _dataStore;
        public ShipmentService()
        {
            _dataStore = DataStore.Instance;
        }

        public async Task<BrokerShipment> GetBrokerShipment(Guid shipmentId)
        {
            return await Task.Run(() => _dataStore.BrokerShipStore[shipmentId]);
        }

        public async Task<Guid> SaveBrokerShipment(BrokerShipment shipment)
        {
            await Task.Run(() =>_dataStore.BrokerShipStore[shipment.ShipmentId] = shipment);
            return shipment.ShipmentId;
        }

        public async Task UpdateBrokerShipment(BrokerShipment shipment)
        {
            await Task.Run(() => _dataStore.BrokerShipStore[shipment.ShipmentId] = shipment);
        }
    }
}