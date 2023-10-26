using System.Diagnostics.CodeAnalysis;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Repositories.Interfaces;
using Altinn.Broker.Core.Services.Interfaces;
using Altinn.Broker.Enums;
using Altinn.Broker.Mappers;
using Altinn.Broker.Models;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers
{
    [ApiController]
    [Route("broker/api/v1/")]
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
        [Route("shipment")]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<ActionResult<BrokerShipmentStatusOverviewExt>> InitializeUploadAndPublishShipmentStreamed([FromForm] IFormFile file,
        [FromForm] BrokerShipmentInitializeExt metadata)
        {
            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            var metadataInit = metadata.MapToInternal();
            var overview = await _shipmentService.InitializeShipment(metadataInit);

            _ = await _fileStorage.SaveFile(overview.ShipmentId, file.OpenReadStream(), metadataInit.Files.First());
            return Accepted(overview.MapToExternal());
        }

        [HttpPost]
        [Consumes("application/json")]
        [Route("shipment/advanced")]
        public async Task<ActionResult<BrokerShipmentStatusOverviewExt>> InitialiseShipment(BrokerShipmentInitializeExt initiateBrokerShipmentRequest)
        {
            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            BrokerShipmentInitialize brokerShipmentInitialize = initiateBrokerShipmentRequest.MapToInternal();
            BrokerShipmentStatusOverview shipmentStatus = await _shipmentService.InitializeShipment(brokerShipmentInitialize);
            return Accepted(shipmentStatus.MapToExternal());
        }

        [HttpGet]
        [Route("shipment/{shipmentId}")]
        public async Task<ActionResult<BrokerShipmentStatusOverviewExt>> GetShipment(Guid shipmentId)
        {
            var shipmentInternal = await _shipmentService.GetShipment(shipmentId);
            return shipmentInternal.MapToExternal();
        }

        [HttpGet]
        [Route("shipment/{shipmentId}/details")]
        public async Task<ActionResult<BrokerShipmentStatusDetailedExt>> GetShipmentDetailed(Guid shipmentId)
        {
            BrokerShipmentStatusDetailed shipmentInternal = await _shipmentService.GetBrokerShipmentDetailed(shipmentId);
            return shipmentInternal.MapToExternal();
        }

        [HttpGet]
        [Route("shipment/hasavailableshipment")]
        public async Task<ActionResult<bool>> HasAvailableShipments(string recipientId)
        {
            await Task.Run(() => 1 == 1);
            if (recipientId is null)
            {
                throw new ArgumentNullException(nameof(recipientId));
            }

            return false;
        }

        [HttpGet]
        [Route("shipment")]
        // Get shipment with filters
        public async Task<ActionResult<List<BrokerShipmentStatusOverviewExt>>> GetShipments([AllowNull] Guid resourceId, [AllowNull] BrokerShipmentStatusExt shipmentStatus, [AllowNull] RoleOnShipmentExt roleOnShipmentExt, [AllowNull] DateTime initiatedDateFrom, [AllowNull] DateTime initiatedDateTo)
        {
            var shipmentInternal = new List<BrokerShipmentStatusOverview>();

            // TODO: validate Parameters.

            // TODO: retrieve shipments based on multiple inputs


            await Task.Run(() => 1 == 1);
            return shipmentInternal.MapToExternal();
        }

        [HttpPost]
        [Route("shipment/{shipmentId}/Cancel")]
        public async Task<ActionResult<BrokerShipmentStatusDetailedExt>> CancelShipment(Guid shipmentId, string reasonText)
        {
            var shipmentInternal = await _shipmentService.CancelShipment(shipmentId, reasonText);
            return Accepted(shipmentInternal.MapToExternal());
        }

        [HttpPost]
        [Consumes("application/json")]
        [Route("shipment/{shipmentId}/report")]
        public async void ReportShipment(Guid shipmentId, string reportText)
        {
            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            await Task.Run(() => 1 == 1);
        }

        [HttpPost]
        [Route("simpleShipment2")]
        public async Task<ActionResult<BrokerShipmentStatusOverviewExt>> InitialiseUploadAndPublishShipment2([FromQuery] Guid brokerResourceId,
        [FromQuery] string sendersShipmentReference, [FromQuery] string[] recipients, [FromQuery] string[] properties, [FromQuery] string fileName, [FromQuery] string checksum, [FromQuery] string sendersFileReference)
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
                foreach (string s in properties)
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
            _ = await _fileStorage.SaveFile(overview.ShipmentId, Request.Body, brokershipinit.Files.First());
            return Accepted(overview.MapToExternal());
        }
    }
}