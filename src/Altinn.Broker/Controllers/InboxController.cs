using Altinn.Broker.Persistence.Models;
using Altinn.Broker.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers
{    
    [ApiController]
    [Route("inbox")]
    public class InboxController : ControllerBase
    {
        private readonly ShipmentRepository _shipmentRepository;
        private readonly ReceiptRepository _receiptRepository;
        public InboxController(ShipmentRepository shipmentRepository, ReceiptRepository receiptRepository)
        {
            _shipmentRepository = shipmentRepository;
            _receiptRepository = receiptRepository;
        }

        [HttpGet]
        [Route("")]
        public async Task<ActionResult<List<Shipment>>> GetInbox()
        {    
            return _shipmentRepository.GetAllShipments();
        }
        
        [HttpGet]
        [Route("hasavailablefiles")]
        public async Task<ActionResult<bool>> HasAvailableFiles()
        {    
            return _shipmentRepository.GetAllShipments().Count > 0;
        }

        [HttpGet]
        [Route("{shipmentId}")]
        public async Task<ActionResult<Shipment>> GetShipment([FromRoute] string shipmentId)
        {    
            return _shipmentRepository.GetShipment(shipmentId);
        }
        
        [HttpGet]
        [Route("{shipmentId}/receipt")]
        public async Task<ActionResult<Receipt>> GetShipmentReceipt([FromRoute] string shipmentId)
        {    
            return _receiptRepository.GetReceipt(shipmentId);
        }
        
        [HttpGet]
        [Route("{shipmentId}/download")]
        public async Task<ActionResult<string>> DownloadShipment([FromRoute] string shipmentId)
        {    
            var shipment = _shipmentRepository.GetShipment(shipmentId);
            return shipment.FileReference;            
        }
        
        [HttpPost]
        [Route("{shipmentId}/confirmdownloaded")]
        public async Task<ActionResult> ConfirmDownloaded([FromRoute] string shipmentId)
        {    
            var receipt = _receiptRepository.GetReceipt(shipmentId);
            receipt.SubReceipts.Add(new SubReceipt(){
                LastChanged = DateTime.Now,
                ParentReceiptID = receipt.ReceiptID,
                PartyReference = "partyReference",
                ReceiptHistory = "receiptHistory",
                ReceiptID = 1 + receipt.SubReceipts.Count,
                SendersReference = receipt.SendersReference,
                Status = "Downloaded",
                Text = "Shipment was confirmed as downloaded"
            });
            _receiptRepository.StoreReceipt(shipmentId, receipt);
            return Ok();
        }
    }
}