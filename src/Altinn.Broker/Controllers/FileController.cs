using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services.Interfaces;
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
        private readonly IFileRepository _fileRepository;
        private readonly IFileStore _fileStore;

        public FileController(IFileRepository fileRepository, IFileStore fileStore)
        {
            _fileRepository = fileRepository;
            _fileStore = fileStore;
        }

        /// <summary>
        /// Initialize a file upload
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<Guid>> InitializeFile(FileInitalizeExt initalizeExt)
        {
            var fileId = await _fileRepository.AddFileAsync(new Core.Domain.File()
            {
                ExternalFileReference = initalizeExt.SendersFileReference,
                FileStatus = Core.Domain.Enums.FileStatus.Initialized,
                FileLocation = "altinn3-blob"
            });

            return Ok(fileId);
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
            var file = await _fileRepository.GetFileAsync(fileId);
            await _fileStore.UploadFile(Request.Body, fileId.ToString(), fileId.ToString());
            await _fileRepository.AddReceiptAsync(new Core.Domain.FileReceipt()
            {
                Actor = new Core.Domain.Actor()
                {
                    ActorExternalId = "0",
                    ActorId = 0
                },
                Date = DateTime.UtcNow,
                FileId = fileId,
                Status = Core.Domain.Enums.ActorFileStatus.Uploaded
            });
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
            var fileId = await _fileRepository.AddFileAsync(new Core.Domain.File()
            {
                ExternalFileReference = form.Metadata.SendersFileReference,
                FileStatus = Core.Domain.Enums.FileStatus.Initialized,
                FileLocation = "altinn3-blob"
            });

            await _fileStore.UploadFile(Request.Body, fileId.ToString(), fileId.ToString());
            await _fileRepository.AddReceiptAsync(new Core.Domain.FileReceipt()
            {
                Actor = new Core.Domain.Actor()
                {
                    ActorExternalId = "0",
                    ActorId = 0
                },
                Date = DateTime.UtcNow,
                FileId = fileId,
                Status = Core.Domain.Enums.ActorFileStatus.Uploaded
            });
            return Ok();
        }

        [HttpGet]
        public async Task<ActionResult<FileStatusOverviewExt>> GetFileStatus(Guid fileId)
        {
            var file = await _fileRepository.GetFileAsync(fileId);
            return Ok(new FileStatusOverviewExt(){
                FileId = fileId
            });
        }

        [HttpGet]
        [Route("{fileId}/download")]
        public async Task<ActionResult> DownloadFile(Guid fileId)
        {
            var file = await _fileRepository.GetFileAsync(fileId);
            if (string.IsNullOrWhiteSpace(file?.FileLocation))
            {
                return BadRequest("No file uploaded yet");
            }
            return Redirect(file.FileLocation);
        }

        [HttpPost]
        [Route("{fileId}/confirmdownload")]
        public async Task<ActionResult> ConfirmDownload(Guid fileId)
        {
            await _fileRepository.AddReceiptAsync(new Core.Domain.FileReceipt()
            {
                Actor = new Core.Domain.Actor()
                {
                    ActorExternalId = "0",
                    ActorId = 0
                },
                Date = DateTime.UtcNow,
                FileId = fileId,
                Status = Core.Domain.Enums.ActorFileStatus.Downloaded
            });
            return Ok();
        }
    }
}