using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Services.Interfaces;

namespace Altinn.Broker.Persistence.MetadataRepository
{
    public class ShipmentMetadataRepository : IShipmentMetadataRepository
    {
        Task<BrokerShipmentMetadata> IShipmentMetadataRepository.GetBrokerShipmentMetadata(Guid shipmentId)
        {
            //TODO: Retreive broker shipment metadata
            throw new NotImplementedException();
        }

        Task<Guid> IShipmentMetadataRepository.SaveBrokerShipmentMetadata(BrokerShipmentMetadata shipment)
        {
            //TODO: 
            throw new NotImplementedException();
        }

        void IShipmentMetadataRepository.SetBrokerShipmentStatus(Guid shipmentId, BrokerShipmentStatus shipmentStatus)
        {
            throw new NotImplementedException();
        }

        void IShipmentMetadataRepository.UpdateBrokerShipmentMetadata(BrokerShipmentMetadata shipment)
        {
            throw new NotImplementedException();
        }
    }
}