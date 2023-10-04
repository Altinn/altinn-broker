using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;

namespace Altinn.Broker.Core.Services.Interfaces
{   
    public interface IShipmentService
    {
        /// <summary>
        /// Retrieves overview of BrokerShipment. Includes current status information.
        /// </summary>
        /// <param name="shipmentId">ShipmentId to retrieve metadata for.</param>
        /// <returns>Shipment Metadata</returns>
        Task<BrokerShipmentStatusOverview> GetBrokerShipmentOverview(Guid shipmentId);

        /// <summary>
        /// Retrieves detailed BrokerShipment data. Includes historical status information.
        /// </summary>
        /// <param name="shipmentId">ShipmentId to retrieve metadata for.</param>
        /// <returns>Shipment Metadata</returns>
        Task<BrokerShipmentStatusDetails> GetBrokerShipmentDetails(Guid shipmentId);

        /// <summary>
        /// Used when initializing a new <see cref="BrokerShipmentMetadata" /> object in BrokerShipment database.
        /// </summary>
        /// <param name="shipment"><see cref="BrokerShipmentMetadata" /> object containing new Metadata.</param>
        /// <returns>ShipmentId for initialized <see cref="BrokerShipmentMetadata" /></returns>
        Task<Guid> InitializeShipment(BrokerShipmentInitialize shipment);

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

        /// <summary>
        /// Used by Shipment sender to make cancel an unpublished BrokerShipment. This deletes all files related to the Shipment.
        /// </summary>
        /// <param name="shipment">ShipmentId to cancel.</param>
        /// <param name="reasonText">Voluntary text detailing why shipment was cancelled.</param>
        /// <returns><see cref="BrokerShipmentMetadata" /></returns>
        Task<BrokerShipmentMetadata> CancelShipment(Guid shipmentId, string reasonText);

        /// <summary>
        /// Marks a given <see cref="BrokerShipmentMetadata" /> to be published as soon as possible.
        /// If a file in the shipment fails anti-virus scanning then the Shipment will not be published.
        /// </summary>
        /// <param name="shipmentId">ShipmentId to publish.</param>
        /// <returns>Shipment Metadata</returns>
        Task<BrokerShipmentMetadata> PublishShipment(Guid shipmentId);
    }    
}