using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;

using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Services.Interfaces;
using Altinn.Broker.Mappers;

namespace Altinn.Broker.Core.Services
{
    public class ShipmentService : IShipmentService
    {
        [AllowNull]
        private static IDataService _dataStore;
        public ShipmentService(IDataService dataService)
        {
            _dataStore = dataService;
        }

        Task<BrokerShipmentStatusDetailed> IShipmentService.CancelShipment(Guid shipmentId, string reasonText)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task<BrokerShipmentStatusOverview> InitializeShipment(BrokerShipmentInitialize shipment)
        {
            BrokerShipmentStatusOverview bsso;
            if(true)
            {
                bsso = new ()
                {
                    BrokerResourceId = shipment.BrokerResourceId,
                    CurrentShipmentStatus = BrokerShipmentStatus.Initialized,
                    CurrentShipmentStatusChanged = DateTime.Now,
                    CurrentShipmentStatusText = "Shipment is initialized and awaiting file uploads.",
                    FileList = shipment.Files.MapToOverview(),
                    Metadata = shipment.Metadata,
                    RecipientStatusList = shipment.Recipients.MapToRecipientStatusOverview(),
                    Sender = shipment.Sender,
                    SendersShipmentReference = shipment.SendersShipmentReference,
                    ShipmentId = Guid.NewGuid(),
                    ShipmentInitialized = DateTime.Now
                };

                await Task.Run(() =>_dataStore.SaveBrokerShipmentStatusOverview(bsso));
            }

            return bsso;
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

        public async Task<BrokerShipmentStatusDetailed> GetBrokerShipmentDetailed(Guid shipmentId)
        {
            await Task.Run(() => 1 == 1);
            throw new NotImplementedException();
        }

        public async Task<BrokerShipmentStatusOverview> GetShipment(Guid shipmentId)
        {
            BrokerShipmentStatusOverview bsso = new BrokerShipmentStatusOverview();
            bsso = _dataStore.GetBrokerShipmentStatusOverview(shipmentId);
            return await Task<BrokerShipmentStatusOverview>.Run(() => _dataStore.GetBrokerShipmentStatusOverview(shipmentId));
        }
    }
}