using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Altinn.Broker.Models;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Mappers;
using Altinn.Broker.Core.Services.Interfaces;
using Altinn.Broker.Persistence;

namespace Altinn.Broker.Controllers
{    
    [ApiController]
    [Route("broker/api/v1/shipment")]
    public class BrokerController : ControllerBase
    {
        private IShipmentService _shipmentService;
        private IFileStore _fileStore;
        [AllowNull]
        private static DataStore store;
        public BrokerController(IShipmentService shipmentService, IFileStore fileStore)
        {
            store = DataStore.Instance;
            _shipmentService = shipmentService;
            _fileStore = fileStore;

        }

        [HttpPost]        
        [Consumes("application/json")]
        public async Task<ActionResult<BrokerShipmentResponseExt>> PostInitiateBrokerShipment(InitiateBrokerShipmentRequestExt initiateBrokerShipmentRequest)
        {
            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            Guid BrokerShipmentIdentifier = Guid.NewGuid();
            BrokerShipment shipmentInternal = initiateBrokerShipmentRequest.MapToBrokerShipment();
            shipmentInternal.Status ="intialized";
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
            if(!store.BrokerShipStore.ContainsKey(shipmentId))
            {
                return StatusCode(404, "shipmentId is not valid");
            }

            var shipmentInternal = await _shipmentService.GetBrokerShipment(shipmentId);
            shipmentInternal.Status = "upload finalized";
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
                FileStatus = "awaiting upload"
            };

            var shipmentInternal = await _shipmentService.GetBrokerShipment(shipmentId);
            string status = $"fileReference {brokerFileMetadata.GetId()} created for sendersref: {brokerFileMetadata.SendersFileReference}, fileName: {brokerFileMetadata.FileName}";
            await _fileStore.UploadFile(Request.Body, shipmentId.ToString(), brokerFileMetadata.GetId());
            brokerFileMetadata.FileStatus = "file uploaded";
            shipmentInternal.Status = "file uploaded";
            shipmentInternal.FileList.Add(brokerFileMetadata);
            return Accepted(brokerFileMetadata);
        }
    }
}