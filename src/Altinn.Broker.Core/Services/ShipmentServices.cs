using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Services.Interfaces;

namespace Altinn.Broker.Core.Services
{
    public class ShipmentServices : IShipmentServices
    {
        [AllowNull]
        private static IDataService _dataStore;
        public ShipmentServices(IDataService dataService)
        {
            _dataStore = dataService;
        }

        public async Task<BrokerShipmentMetadata> GetBrokerShipment(Guid shipmentId)
        {
            return await Task.Run(() => _dataStore.GetBrokerShipmentMetadata(shipmentId));
        }

        public async Task<Guid> SaveBrokerShipment(BrokerShipmentMetadata shipment)
        {
            await Task.Run(() => _dataStore.SaveBrokerShipmentMetadata(shipment));
            return shipment.ShipmentId;
        }

        public async Task UpdateBrokerShipment(BrokerShipmentMetadata shipment)
        {
            await Task.Run(() => _dataStore.SaveBrokerShipmentMetadata(shipment));
        }
    }
}