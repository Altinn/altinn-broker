using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;

namespace Altinn.Broker.Core.Services.Interfaces
{   
    public interface IShipmentMetadataRepository
    {
        /// <summary>
        /// Retrieves <see cref="BrokerShipmentMetadata" /> from Metadata database.
        /// Should also contain list of <see cref="BrokerFileMetadata" />.
        /// </summary>
        /// <param name="shipmentId">ShipmentId to retrieve metadata for.</param>
        /// <returns>Shipment Metadata</returns>
        Task<BrokerShipmentMetadata> GetBrokerShipmentMetadata(Guid shipmentId);

        /// <summary>
        /// Used when initializing a new <see cref="BrokerShipmentMetadata" /> object in Metadata database.
        /// </summary>
        /// <param name="shipment"><see cref="BrokerShipmentMetadata" /> object containing new Metadata.</param>
        /// <returns>ShipmentId for initialized <see cref="BrokerShipmentMetadata" /></returns>
        Task<Guid> SaveBrokerShipmentMetadata(BrokerShipmentMetadata shipment);

        /// <summary>
        /// Used internally when updating status of <see cref="BrokerShipmentMetadata" />.
        /// </summary>
        /// <param name="shipmentId">Id of shipment to update status for.</param>
        /// <param name="shipmentStatus">New status update.</param>
        void SetBrokerShipmentStatus(Guid shipmentId, BrokerShipmentStatus shipmentStatus);

        /// <summary>
        /// Used by Shipment sender to make changes to <see cref="BrokerShipmentMetadata" />.
        /// </summary>
        /// <param name="shipment">New Metadata to update existing Metadata object.</param>
        /// <returns></returns>
        void UpdateBrokerShipmentMetadata(BrokerShipmentMetadata shipment);
    }    
}