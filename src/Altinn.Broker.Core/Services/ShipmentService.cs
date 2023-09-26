using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;

using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Services.Interfaces;

namespace Altinn.Broker.Core.Services
{
    public class ShipmentService : IShipmentService
    {
        [AllowNull]
        private static DataStore _dataStore;

        public Task<BrokerShipmentMetadata> CancelShipment(Guid shipmentId, string reasonText)
        {
            // TODO: Cancel shipment.
            // TODO: Cancel each file in shipment.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<BrokerShipmentMetadata> GetBrokerShipmentMetadata(Guid shipmentId)
        {
            throw new NotImplementedException();
        }

        public Task<BrokerShipmentMetadata> PublishShipment(Guid shipmentId)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<Guid> SaveBrokerShipmentMetadata(BrokerShipmentMetadata shipment)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void SetBrokerShipmentStatus(Guid shipmentId, BrokerShipmentStatus shipmentStatus)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void UpdateBrokerShipmentMetadata(BrokerShipmentMetadata shipment)
        {
            throw new NotImplementedException();
        }
    }
}