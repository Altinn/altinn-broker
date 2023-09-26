using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Altinn.Broker.Models;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Mappers;
using Altinn.Broker.Core.Services.Interfaces;
using Altinn.Broker.Persistence;
using Altinn.Broker.Core.Enums;

namespace Altinn.Broker.Controllers
{    
    [ApiController]
    [Route("broker/api/v1/shipment")]
    public class BrokerController : ControllerBase
    {
        private readonly IShipmentServices _shipmentService;
        private readonly IFileStore _fileStore;
        public BrokerController(IShipmentServices shipmentService, IFileStore fileStore)
        {
            _shipmentService = shipmentService;
            _fileStore = fileStore;
        }

        [HttpPost]        
        [Consumes("application/json")]
        public async Task<ActionResult<BrokerShipmentResponseExt>> PostInitiateBrokerShipment(InitiateBrokerShipmentRequestExt initiateBrokerShipmentRequest)
        {
            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            Guid BrokerShipmentIdentifier = Guid.NewGuid();
            BrokerShipmentMetadata shipmentInternal = initiateBrokerShipmentRequest.MapToBrokerShipment();
            shipmentInternal.Status = BrokerShipmentStatus.Initialized;
            shipmentInternal.ShipmentId = BrokerShipmentIdentifier;
            BrokerShipmentIdentifier = await _shipmentService.SaveBrokerShipment(shipmentInternal);
            shipmentInternal.ShipmentId = BrokerShipmentIdentifier;
            return shipmentInternal.MapToBrokerShipmentExtResponse();
        }
        
        [HttpPut]
        [Route("{shipmentId}")]
        [Consumes("application/json")]
        public async Task<ActionResult<BrokerShipmentResponseExt>> PutFinalizeBrokerShipmentUpload(Guid shipmentId)
        {
            // This method should allow enduser to "finalize" file upload, which should tell altinn that the file package is now fully uploaded. (Should this be necessary)

            var shipmentInternal = await _shipmentService.GetBrokerShipment(shipmentId);
            if(shipmentInternal is null)
            {
                return StatusCode(404, "shipmentId is not valid");
            }
            shipmentInternal.Status = BrokerShipmentStatus.Published;
            await _shipmentService.UpdateBrokerShipment(shipmentInternal);

            return Accepted(shipmentInternal.MapToBrokerShipmentExtResponse());
        }
        
        [HttpPost]        
        [Route("{shipmentId}")]        
        public async Task<ActionResult<BrokerFileMetadataExt>> PostUploadFileToBrokerShipment(Guid shipmentId, string sendersFileReference, [AllowNull] string fileName)
        {
            // Simple operation that uploads a file to the given broker shipment id
            var brokerShipment = await _shipmentService.GetBrokerShipment(shipmentId);
            if(brokerShipment == null)
            {
                return StatusCode(404, "shipmentId is not valid");
            }

            BrokerFileMetadata brokerFileMetadata = new BrokerFileMetadata()
            {
                FileId = Guid.NewGuid(),
                ShipmentId = shipmentId,
                SendersFileReference = sendersFileReference,
                FileName = fileName ?? string.Empty,
                FileStatus = BrokerFileStatus.Uploaded
            };

            var shipmentInternal = await _shipmentService.GetBrokerShipment(shipmentId);
            await _fileStore.UploadFile(Request.Body, shipmentId.ToString(), brokerFileMetadata.GetId());
            brokerFileMetadata.FileStatus = BrokerFileStatus.Uploaded;
            shipmentInternal.Status = BrokerShipmentStatus.RequiresSenderInteraction;
            shipmentInternal.FileList.Add(brokerFileMetadata);
            return Accepted(brokerFileMetadata);
        }
    }
}