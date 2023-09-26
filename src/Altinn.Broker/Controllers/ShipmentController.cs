using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Altinn.Broker.Models;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Mappers;
using Altinn.Broker.Core.Services.Interfaces;
using Altinn.Broker.Persistence;

namespace Altinn.Broker.Controllers
{    
    [ApiController]
    [Route("broker/api/v1.1/")]
    public class ShipmentController : ControllerBase
    {
        private readonly IShipmentService _shipmentService;
        private readonly IFileStore _fileStore;
        public ShipmentController(IShipmentService shipmentService, IFileStore fileStore)
        {
            _shipmentService = shipmentService;
            _fileStore = fileStore;
        }

        [HttpPost]        
        [Consumes("application/json")]
        [Route("outbox/simpleShipment")]
        public async Task<ActionResult<BrokerShipmentResponseExt>> InitialiseUploadAndPublishShipment(InitiateBrokerShipmentRequestExt initiateBrokerShipmentRequest)
        {
            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.

            //TODO: HANDLE INIT, UPLOAD AND PuBLISH Shipment and file in single call.
            BrokerShipmentMetadata brokerShipmentMetadata = initiateBrokerShipmentRequest.MapToBrokerShipment();
            Guid BrokerShipmentIdentifier = await _shipmentService.SaveBrokerShipmentMetadata(brokerShipmentMetadata);
            brokerShipmentMetadata.ShipmentId = BrokerShipmentIdentifier;
            return brokerShipmentMetadata.MapToBrokerShipmentExtResponse();
        }

        [HttpPost]        
        [Consumes("application/json")]
        [Route("outbox/shipment")]
        public async Task<ActionResult<BrokerShipmentResponseExt>> InitialiseShipment(InitiateBrokerShipmentRequestExt initiateBrokerShipmentRequest)
        {
            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            BrokerShipmentMetadata brokerShipmentMetadata = initiateBrokerShipmentRequest.MapToBrokerShipment();
            Guid BrokerShipmentIdentifier = await _shipmentService.SaveBrokerShipmentMetadata(brokerShipmentMetadata);
            brokerShipmentMetadata.ShipmentId = BrokerShipmentIdentifier;
            return brokerShipmentMetadata.MapToBrokerShipmentExtResponse();
        }

        [HttpGet]
        [Route("outbox/shipment")]
        public async Task<ActionResult<BrokerShipmentResponseExt>> GetShipmentMetadata(Guid shipmentId)
        {
            var shipmentInternal = await _shipmentService.GetBrokerShipmentMetadata(shipmentId);
            return shipmentInternal.MapToBrokerShipmentExtResponse();
        }

        [HttpGet]
        [Route("outbox/shipment")]
        public async Task<ActionResult<BrokerShipmentResponseExt>> GetShipments([AllowNull] Guid resourceId,[AllowNull] string shipmentStatus,[AllowNull] DateTime initiatedDateFrom,[AllowNull] DateTime initiatedDateTo)
        {
            var shipmentInternal = new BrokerShipmentMetadata();
            // TODO: validate Parameters.

            // TODO: retrieve shipments based on multiple inputs
            
            return shipmentInternal.MapToBrokerShipmentExtResponse();
        }
        
        [HttpPost]
        [Route("outbox/shipment/{shipmentId}/Publish")]
        [Consumes("application/json")]
        public async Task<ActionResult<BrokerShipmentResponseExt>> PublishShipment(Guid shipmentId)
        {
            // This method should allow enduser to "finalize" file upload, which should tell altinn that the file package is now fully uploaded. (Should this be necessary)
            var shipmentInternal = await _shipmentService.PublishShipment(shipmentId);
            return Accepted(shipmentInternal.MapToBrokerShipmentExtResponse());
        }

        [HttpPost]
        [Route("outbox/Shipment/{shipmentId}/Cancel")]
        public async Task<ActionResult<BrokerShipmentResponseExt>> CancelShipment(Guid shipmentId, string reasonText)
        {
            var shipmentInternal = await _shipmentService.CancelShipment(shipmentId, reasonText);
            return Accepted(shipmentInternal.MapToBrokerShipmentExtResponse());
        }
    }
}