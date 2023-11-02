using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Models;
using Altinn.Broker.Persistence;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers
{
    [ApiController]
    [Route("broker/api/v1/file")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class FileController : ControllerBase
    {
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
            var caller = GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }
            var fileId = await _fileRepository.AddFileAsync(new Core.Domain.File()
            {
                ExternalFileReference = initalizeExt.SendersFileReference,
                FileStatus = Core.Domain.Enums.FileStatus.Initialized,
                FileLocation = "altinn3-blob",
                LastStatusUpdate = DateTimeOffset.UtcNow,
                Receipts = new List<Core.Domain.FileReceipt>(),
            });

            await _fileRepository.AddReceiptAsync(new Core.Domain.FileReceipt(){
                FileId = fileId,
                Status = Core.Domain.Enums.ActorFileStatus.Initialized,
                Actor = new Core.Domain.Actor(){
                    ActorExternalId = caller ?? "default"
                }
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
            var caller = GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }
            var file = await _fileRepository.GetFileAsync(fileId);
            await _fileStore.UploadFile(Request.Body, fileId);
            await _fileRepository.AddReceiptAsync(new Core.Domain.FileReceipt()
            {
                Actor = new Core.Domain.Actor()
                {
                    ActorExternalId = caller ?? "default",
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
            var caller = GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }
            var fileId = await _fileRepository.AddFileAsync(new Core.Domain.File()
            {
                ExternalFileReference = form.Metadata.SendersFileReference,
                FileStatus = Core.Domain.Enums.FileStatus.Initialized,
                FileLocation = "altinn3-blob"
            });

            await _fileStore.UploadFile(Request.Body, fileId);
            await _fileRepository.AddReceiptAsync(new Core.Domain.FileReceipt()
            {
                Actor = new Core.Domain.Actor()
                {
                    ActorExternalId = caller ?? "default",
                    ActorId = 0
                },
                Date = DateTime.UtcNow,
                FileId = fileId,
                Status = Core.Domain.Enums.ActorFileStatus.Uploaded
            });
            return Ok();
        }

        [HttpGet]
        [Route("{fileId}")]
        public async Task<ActionResult<FileStatusOverviewExt>> GetFileStatus(Guid fileId)
        {
            var file = await _fileRepository.GetFileAsync(fileId);
            return Ok(new FileStatusOverviewExt(){
                FileId = fileId
            });
        }

        [HttpGet]
        [Route("{fileId}/details")]
        public async Task<ActionResult<FileStatusDetailsExt>> GetFileDetails(Guid fileId)
        {
            var fileHistory = _fileRepository.GetFileAsync(fileId);
            throw new NotImplementedException();
        }

        [HttpGet]
        public async Task<ActionResult<List<string>>> GetFiles()
        {
            var caller = GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }
            var files = _fileRepository.GetFilesAsync(caller);
            return Ok(files);
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
            var caller = GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }
            await _fileRepository.AddReceiptAsync(new Core.Domain.FileReceipt()
            {
                Actor = new Core.Domain.Actor()
                {
                    ActorExternalId = caller ?? "default",
                    ActorId = 0
                },
                Date = DateTime.UtcNow,
                FileId = fileId,
                Status = Core.Domain.Enums.ActorFileStatus.Downloaded
            });
            return Ok();
        }

        private string? GetCallerFromTestToken(HttpContext httpContext) => httpContext.User.Claims.FirstOrDefault(claim => claim.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}