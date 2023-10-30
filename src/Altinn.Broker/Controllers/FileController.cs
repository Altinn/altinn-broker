using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Repositories.Interfaces;
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
        private readonly IFileStore _fileStore;

        public FileController(IFileService fileService, IFileStore fileStore)
        {
            _fileService = fileService;
            _fileStore = fileStore;
        }

        /// <summary>
        /// Initialize a file upload
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<Guid>> InitializeFile(FileInitalizeExt initalizeExt)
        {
            BrokerFileStatusOverview brokerFileMetadata = await _fileService.UploadFile(Guid.NewGuid(), Guid.NewGuid(), Request.Body);

            return Ok(brokerFileMetadata.FileId);
        }
        
        /// <summary>
        /// Upload to an initialized file using a binary stream.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{fileId}/upload")]
        public async Task<ActionResult> UploadFileStreamed(
            Guid fileId)
        {
            await _fileStore.UploadFile(Request.Body, fileId.ToString(), fileId.ToString());
            return Ok();
        }
        
        /// <summary>
        /// Initialize and upload a file using form-data
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("upload")]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<ActionResult> InitializeAndUpload(
            [FromForm] FileInitializeAndUploadExt form
        )
        {
            // This method should initiate a "broker shipment" that will allow enduser to upload file, similar to Altinn 2 Soap operation.
            //var metadataInit = form.Metadata.MapToInternal();

            //BrokerFileStatusOverview brokerFileMetadata = await _fileService.UploadFile(Guid.NewGuid(), Guid.NewGuid(), form.File.OpenReadStream());
            //return Accepted(brokerFileMetadata.MapToExternal());
            return Ok();
        }

        /// <summary>
        /// Publish an uploaded file
        /// </summary>
        /// <returns></returns>
        /*[HttpPost]
        [Route("{fileId}/publish")]
        public async Task<ActionResult> PublishFile(
            Guid fileId)
        {
            return Ok();
        }*/

        [HttpGet]
        public async Task<ActionResult<FileStatusOverviewExt>> GetFileStatus(Guid fileId)
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

        [HttpPost]
        [Route("{fileId}/confirmdownload")]
        public async Task<ActionResult> ConfirmDownload(Guid fileId)
        {
            await Task.Run(() => 1 == 1);
            return Accepted();
        }
    }
}