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
        [Route("outbox/simpleShipment")]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<ActionResult<BrokerShipmentResponseExt>> InitialiseUploadAndPublishShipment([FromForm] IFormFile file, 
        [FromForm] InitiateBrokerShipmentRequestExt metadata)
        {
            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            BrokerShipmentMetadata brokerShipmentMetadata = metadata.MapToBrokerShipment();
            brokerShipmentMetadata.ShipmentId = Guid.NewGuid();
            string targetDirectory = $@"C:\Temp\testfolderrestapi\{brokerShipmentMetadata.ShipmentId}";
            if(!Path.Exists(targetDirectory))
            {
                System.IO.Directory.CreateDirectory(targetDirectory);
            }

            using(FileStream fs = new FileStream($@"{targetDirectory}\{file.FileName}", FileMode.CreateNew))
            {
                await file.CopyToAsync(fs);
            }

            // Guid BrokerShipmentIdentifier = await _shipmentService.SaveBrokerShipmentMetadata(brokerShipmentMetadata);
            //brokerShipmentMetadata.ShipmentId = BrokerShipmentIdentifier;
            GC.Collect();
            return Accepted(brokerShipmentMetadata.MapToBrokerShipmentExtResponse());
        }

        [HttpPost]        
        [Route("outbox/simpleShipment2")]
        public async Task<ActionResult<BrokerShipmentResponseExt>> InitialiseUploadAndPublishShipment2([FromQuery] string serviceCode, 
        [FromQuery] string serviceEditionCode, [FromQuery] string sendersReference,[FromQuery] string[] recipients,[FromQuery] string[] properties, [FromQuery] string fileName)
        {
            InitiateBrokerShipmentRequestExt metadata = new InitiateBrokerShipmentRequestExt()
            {
                ServiceCode = serviceCode, Recipients = recipients.ToList(), ServiceEditionCode = int.Parse(serviceEditionCode), SendersReference = sendersReference            

            };
            metadata.Properties = new Dictionary<string, string>();

            if(properties != null && properties.Count() > 0)
            {
                foreach(string s in properties)
                {
                    metadata.Properties[s.Split(":")[0]] = s.Split(":")[1];
                }
            }

            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            BrokerShipmentMetadata brokerShipmentMetadata = metadata.MapToBrokerShipment();
            brokerShipmentMetadata.ShipmentId = Guid.NewGuid();
            string targetDirectory = $@"C:\Temp\testfolderrestapi\{brokerShipmentMetadata.ShipmentId}";
            if(!Path.Exists(targetDirectory))
            {
                System.IO.Directory.CreateDirectory(targetDirectory);
            }

            using(FileStream fs = new FileStream($@"{targetDirectory}\{fileName}", FileMode.CreateNew))
            {
                await Request.Body.CopyToAsync(fs);
            }

            // Guid BrokerShipmentIdentifier = await _shipmentService.SaveBrokerShipmentMetadata(brokerShipmentMetadata);
            //brokerShipmentMetadata.ShipmentId = BrokerShipmentIdentifier;
            return Accepted(brokerShipmentMetadata.MapToBrokerShipmentExtResponse());
        }

        [HttpPost]        
        [Consumes("application/json")]
        [Route("outbox/shipment")]
        public async Task<ActionResult<Guid>> InitialiseShipment(BrokerShipmentInitializeExt initiateBrokerShipmentRequest)
        {
            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            BrokerShipmentInitialize brokerShipmentInitialize = initiateBrokerShipmentRequest.MapToBrokerShipmentInitialize();
            Guid shipmentId = await _shipmentService.InitializeShipment(brokerShipmentInitialize);
            return shipmentId;
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
        public async Task<ActionResult<BrokerShipmentResponseExt>> GetShipments([AllowNull] Guid resourceId,[AllowNull] string shipmentStatus,[AllowNull] DateTime initiatedDateFrom,[AllowNull] DateTime initiatedDateTo)
        {
            var shipmentInternal = new BrokerShipmentMetadata();
            // TODO: validate Parameters.

            // TODO: retrieve shipments based on multiple inputs
            
            return shipmentInternal.MapToBrokerShipmentExtResponse();
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