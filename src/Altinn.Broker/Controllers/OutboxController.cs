using System.Text.Json;

using Altinn.Broker.Models;
using Altinn.Broker.Persistence;
using Altinn.Broker.Persistence.Models;
using Altinn.Broker.Persistence.Repositories;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers
{    
    [ApiController]
    [Route("outbox")]
    public class OutboxController : ControllerBase
    {
        private readonly IFileStore _fileStore;
        private readonly ShipmentRepository _shipmentRepository;
        private readonly ReceiptRepository _receiptRepository;

        public OutboxController(IFileStore fileStore, ShipmentRepository shipmentRepository, ReceiptRepository receiptRepository) 
        {
            _fileStore = fileStore;
            _shipmentRepository = shipmentRepository;
            _receiptRepository = receiptRepository;
        }

        [HttpPost]        
        [Consumes("application/zip")]
        public async Task<ActionResult> UploadShipment([FromQuery] string brokerServiceDescription, [FromQuery] string? fileName)
        {
            var shipmentId = Guid.NewGuid();
            var actualFileName = fileName ?? shipmentId + ".zip";
            var shipment = new Shipment(){
                Id = shipmentId.ToString(),
                BrokerServiceDescription = brokerServiceDescription,
                Filename = actualFileName
            };
            _shipmentRepository.StoreShipment(shipmentId.ToString(), shipment);
            var fileReference = await _fileStore.UploadFile(Request.Body, shipmentId.ToString(), actualFileName);
            shipment.FileReference = fileReference;
            _shipmentRepository.StoreShipment(shipmentId.ToString(), shipment);
            _receiptRepository.StoreReceipt(shipmentId.ToString(), new Receipt(){
                LastChanged = DateTime.Now,
                OwnerPartyReference = "ownerPartyReference",
                ParentReceiptID = 0,
                PartyReference = "partyReference",
                ReceiptID = 0,
                SendersReference = "sendersReference",
                Status = "Ready",
                Text = "Shipment is ready for download",
                SubReceipts = new List<SubReceipt>()
            });
            return Accepted();
        }

        [HttpGet]
        [Route("{shipmentId}")]
        public async Task<ActionResult<ShipmentMetadata>> GetShipmentMetadata([FromRoute] string shipmentId)
        {    
            var shipment = _shipmentRepository.GetShipment(shipmentId);
            var brokerServiceDescription = JsonSerializer.Deserialize<BrokerServiceDescription>(shipment.BrokerServiceDescription);
            return new ShipmentMetadata(){
                FileName = shipment.Filename,
                SendersReference = brokerServiceDescription?.SendersReference ?? "",
                FileReference = shipment.FileReference
            };
        }

        [HttpGet]
        [Route("{shipmentId}/receipt")]
        public async Task<ActionResult<Receipt>> GetReceipt([FromRoute] string shipmentId)
        {    
            return _receiptRepository.GetReceipt(shipmentId);
        }
    }
}