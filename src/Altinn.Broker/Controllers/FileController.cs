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
    [Route("broker/api/v1.1/outbox/file")]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;
        public FileController(IFileService fileService)
        {
            _fileService = fileService;
        }

        /// <summary>
        /// Upload a file using a binary stream.
        /// </summary>
        /// <param name="shipmentId">ShipmentId - identifies the shipment that the file belongs to.</param>
        /// <param name="fileName">The name of the file being uploaded.</param>
        /// <param name="sendersFileReference">External reference for the file.</param>
        /// <param name="checksum">Checksum for the file.</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<BrokerShipmentResponseExt>> UploadFileStreamed(
            Guid shipmentId, 
            string fileName,
            string sendersFileReference,
            string checksum)
        {
            BrokerFileMetadata brokerFileMetadata = await _fileService.UploadFile(shipmentId, Request.Body, fileName, sendersFileReference, checksum);
            return Accepted(brokerFileMetadata.MapToBrokerFileMetadataExt());
        }

        [HttpPost]
        [Route("{fileId}/resume")]
        public async Task<ActionResult<BrokerShipmentResponseExt>> ResumeUploadFileStreamed(
            Guid fileId, 
            Guid shipmentId,
            string fileName,
            string sendersFileReference,
            string checksum)
        {
            BrokerFileMetadata brokerFileMetadata = await _fileService.ResumeUploadFile(shipmentId, fileId, Request.Body, fileName, sendersFileReference, checksum);
            return Accepted(brokerFileMetadata.MapToBrokerFileMetadataExt());
        }

        [HttpGet]
        public async Task<ActionResult<object>> GetFileStatus(Guid fileId)
        {
            BrokerFileMetadata brokerFileMetadata = await _fileService.GetBrokerFileMetadata(fileId);
            return Accepted(brokerFileMetadata.MapToBrokerFileMetadataExt());
        }

        [HttpPut]
        public async Task<ActionResult<BrokerShipmentResponseExt>> OverwriteFileStreamed(Guid fileId, 
        string fileName, 
        string sendersFileReference, 
        string checksum)
        {
            BrokerFileMetadata md = await _fileService.OverwriteFile(fileId, Request.Body, fileName, sendersFileReference, checksum);
            return Accepted(md.MapToBrokerFileMetadataExt());
        }

        [HttpPost]
        [Route("{fileId}/cancel")]
        public async Task<ActionResult<object>> CancelFile(Guid fileId)
        {
            BrokerFileMetadata md = await _fileService.CancelFile(fileId);
            return Accepted(md.MapToBrokerFileMetadataExt());
        }
    }
}