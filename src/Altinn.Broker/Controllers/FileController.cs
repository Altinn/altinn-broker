using System.Diagnostics.CodeAnalysis;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Services.Interfaces;
using Altinn.Broker.Mappers;
using Altinn.Broker.Models;
using Altinn.Broker.Persistence;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers
{
    [ApiController]
    [Route("broker/api/v1/file")]
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
        public async Task<ActionResult<Guid>> UploadFileStreamed(
            Guid shipmentId,
            Guid fileId)
        {
            BrokerFileStatusOverview brokerFileMetadata = await _fileService.UploadFile(shipmentId, fileId, Request.Body);
            return Accepted(brokerFileMetadata.FileId);
        }

        [HttpGet]
        public async Task<ActionResult<BrokerFileStatusOverviewExt>> GetFileStatus(Guid fileId)
        {
            BrokerFileStatusOverview brokerFileStatusOverview = await _fileService.GetFileStatus(fileId);
            return Accepted(brokerFileStatusOverview.MapToExternal());
        }

        [HttpGet]
        [Route("{fileId}/download")]
        public async Task<Stream> DownloadFile(Guid fileId)
        {
            await Task.Run(() => 1 == 1);
            MemoryStream ms = new MemoryStream();
            return ms;
        }

        [HttpGet]
        [Route("{fileId}/confirm")]
        public async Task<ActionResult> ConfirmDownload(Guid fileId)
        {
            await Task.Run(() => 1 == 1);
            return Accepted();
        }

        [HttpPut]
        public async Task<ActionResult<BrokerFileStatusOverviewExt>> OverwriteFileStreamed(Guid fileId,
        string fileName,
        string sendersFileReference,
        string checksum)
        {
            await Task.Run(() => 1 == 1);
            BrokerFileStatusOverviewExt brokerFileStatusOverviewExt = new BrokerFileStatusOverviewExt();
            return Accepted(brokerFileStatusOverviewExt);
        }

        [HttpPost]
        [Route("{fileId}/cancel")]
        public async Task<ActionResult> CancelFile(Guid fileId, string reasonText)
        {
            await Task.Run(() => 1 == 1);
            BrokerFileStatusOverviewExt brokerFileStatusOverviewExt = new BrokerFileStatusOverviewExt();
            return Accepted();
        }

        [HttpPost]
        [Route("{fileId}/Report")]
        public async void ReportFile(Guid fileId, string reportText)
        {
            await Task.Run(() => 1 == 1);
            BrokerFileStatusOverviewExt brokerFileStatusOverviewExt = new BrokerFileStatusOverviewExt();
        }

        [HttpPost]
        [Route("{fileId}/resume")]
        public async Task<ActionResult<Guid>> ResumeUploadFileStreamed(
            Guid fileId,
            Guid shipmentId,
            string fileName,
            string sendersFileReference,
            string checksum)
        {
            BrokerFileMetadata brokerFileMetadata = await _fileService.ResumeUploadFile(shipmentId, fileId, Request.Body, fileName, sendersFileReference, checksum);
            return Accepted(brokerFileMetadata.FileId);
        }
    }
}