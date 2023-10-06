using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Altinn.Broker.Models;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Mappers;
using Altinn.Broker.Core.Services.Interfaces;
using Altinn.Broker.Persistence;
using Microsoft.AspNetCore.Http.Extensions;
using System.Numerics;
using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Repositories.Interfaces;

namespace Altinn.Broker.Controllers
{    
    [ApiController]
    [Route("broker/api/v1.1/")]
    public class ShipmentController : ControllerBase
    {
        private readonly IShipmentService _shipmentService;
        private readonly IFileStorage _fileStorage;
        public ShipmentController(IShipmentService shipmentService, IFileStorage fileStorage)
        {
            _shipmentService = shipmentService;
            _fileStorage = fileStorage;
        }

        [HttpPost]        
        [Route("outbox/simpleShipment")]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<ActionResult<BrokerShipmentStatusOverviewExt>> InitialiseUploadAndPublishShipment([FromForm] IFormFile file, 
        [FromForm] BrokerShipmentInitializeExt metadata)
        {
            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            var metadataInit = metadata.MapToBrokerShipmentInitialize();
            var overview = await _shipmentService.InitializeShipment(metadataInit);
            
            _ = await _fileStorage.SaveFile(overview.ShipmentId, file.OpenReadStream(), metadataInit.Files.First());
            return Accepted(overview.MapToOverviewExternal());
        }

        [HttpPost]        
        [Route("outbox/simpleShipment2")]
        public async Task<ActionResult<BrokerShipmentStatusOverviewExt>> InitialiseUploadAndPublishShipment2([FromQuery] Guid brokerResourceId, 
        [FromQuery] string sendersShipmentReference,[FromQuery] string[] recipients,[FromQuery] string[] properties, [FromQuery] string fileName, [FromQuery] string checksum, [FromQuery] string sendersFileReference)
        {
            BrokerShipmentInitialize brokershipinit = new BrokerShipmentInitialize
            {
                BrokerResourceId = brokerResourceId,
                Recipients = recipients.ToList(),
                SendersShipmentReference = sendersShipmentReference,
                Metadata = new Dictionary<string, string>()
            };

            if (properties != null && properties.Count() > 0)
            {
                foreach(string s in properties)
                {
                    brokershipinit.Metadata[s.Split(":")[0]] = s.Split(":")[1];
                }
            }

            brokershipinit.Files = new List<BrokerFileInitalize>
            {
                new() { Checksum = checksum, FileName = fileName, SendersFileReference = sendersFileReference }
            };

            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            var overview = await _shipmentService.InitializeShipment(brokershipinit);
            _ =  await _fileStorage.SaveFile(overview.ShipmentId, Request.Body, brokershipinit.Files.First());
            return Accepted(overview.MapToOverviewExternal());
        }

        [HttpPost]        
        [Consumes("application/json")]
        [Route("outbox/shipment")]
        public async Task<ActionResult<BrokerShipmentStatusOverviewExt>> InitialiseShipment(BrokerShipmentInitializeExt initiateBrokerShipmentRequest)
        {
            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            BrokerShipmentInitialize brokerShipmentInitialize = initiateBrokerShipmentRequest.MapToBrokerShipmentInitialize();
            BrokerShipmentStatusOverview shipmentStatus = await _shipmentService.InitializeShipment(brokerShipmentInitialize);
            return Accepted(shipmentStatus.MapToOverviewExternal());
        }

        [HttpGet]
        [Route("outbox/shipment/{shipmentId}")]
        public async Task<ActionResult<BrokerShipmentStatusOverviewExt>> GetShipmentOverview(Guid shipmentId)
        {
            var shipmentInternal = await _shipmentService.GetBrokerShipmentOverview(shipmentId);
            return shipmentInternal.MapToOverviewExternal();
        }

        [HttpGet]
        [Route("outbox/shipment/{shipmentId}/details")]
        public async Task<ActionResult<BrokerShipmentStatusDetailsExt>> GetShipmentDetails(Guid shipmentId)
        {
            var shipmentInternal = await _shipmentService.GetBrokerShipmentDetails(shipmentId);
            return shipmentInternal.MapToDetailsExternal();
        }

        [HttpGet]
        [Route("outbox/shipment")]
        public async Task<ActionResult<BrokerShipmentStatusOverviewExt>> GetShipmentDetails(Guid resourceId, BrokerShipmentStatus shipmentStatus, DateTime initFrom, DateTime initTo)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        [Route("outbox/shipment")]
        public async Task<ActionResult<List<BrokerShipmentStatusOverviewExt>>> GetShipments([AllowNull] Guid resourceId,[AllowNull] string shipmentStatus,[AllowNull] DateTime initiatedDateFrom,[AllowNull] DateTime initiatedDateTo)
        {
            var shipmentInternal = new BrokerShipmentMetadata();
            // TODO: validate Parameters.

            // TODO: retrieve shipments based on multiple inputs
            
            
            return new List<BrokerShipmentStatusOverviewExt> () {shipmentInternal.MapToBrokerShipmentExtResponse()};
        }

        [HttpPost]
        [Route("outbox/Shipment/{shipmentId}/Cancel")]
        public async Task<ActionResult<BrokerShipmentStatusOverviewExt>> CancelShipment(Guid shipmentId, string reasonText)
        {
            var shipmentInternal = await _shipmentService.CancelShipment(shipmentId, reasonText);
            return Accepted(shipmentInternal.MapToOverviewExternal());
        }
    }
}