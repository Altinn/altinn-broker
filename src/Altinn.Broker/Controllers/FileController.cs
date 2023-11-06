using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Mappers;
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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FileController> _logger;

        public FileController(IFileRepository fileRepository, IFileStore fileStore, IHttpClientFactory httpClientFactory, ILogger<FileController> logger)
        {
            _fileRepository = fileRepository;
            _fileStore = fileStore;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Initialize a file upload
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<Guid>> InitializeFile(FileInitalizeExt initializeExt)
        {
            var caller = GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }

            var file = FileInitializeExtMapper.MapToDomain(initializeExt, caller);
            var fileId = await _fileRepository.AddFileAsync(file, caller);

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
            _logger.LogInformation("File size is {FileSize]} bytes", Request.ContentLength);
            var caller = GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }
            var file = await _fileRepository.GetFileAsync(fileId);
            if (file is null)
            {
                return BadRequest();
            }

            using var bodyStream = HttpContext.Request.BodyReader.AsStream(true);
            await _fileStore.UploadFile(bodyStream, fileId);
            await _fileRepository.SetStorageReference(fileId, "altinn-3-" + fileId.ToString());
            await _fileRepository.AddReceipt(fileId, ActorFileStatus.Uploaded, file.Sender);

            return Ok(fileId);
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

            var file = FileInitializeExtMapper.MapToDomain(form.Metadata, caller);
            var fileId = await _fileRepository.AddFileAsync(file, caller);
            await _fileStore.UploadFile(form.File.OpenReadStream(), fileId);
            await _fileRepository.SetStorageReference(fileId, "altinn-3-" + fileId.ToString());
            await _fileRepository.AddReceipt(fileId, ActorFileStatus.Uploaded, file.Sender);

            return Ok(fileId);
        }

        /// <summary>
        /// Get information about the file and its current status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}")]
        public async Task<ActionResult<FileOverviewExt>> GetFileStatus(Guid fileId)
        {
            var file = await _fileRepository.GetFileAsync(fileId);
            if (file is null)
            {
                return NotFound();
            }

            return Ok(FileStatusOverviewExtMapper.MapToExternalModel(file));
        }

        /// <summary>
        /// Get more detailed information about the file upload for auditing and troubleshooting purposes
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}/details")]
        public async Task<ActionResult<FileStatusDetailsExt>> GetFileDetails(Guid fileId)
        {
            var caller = GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }
            var file = await _fileRepository.GetFileAsync(fileId);
            if (file is null)
            {
                return NotFound();
            }

            var fileHistory = await _fileRepository.GetFileStatusHistoryAsync(fileId);
            var recipientHistory = await _fileRepository.GetFileRecipientStatusHistoryAsync(fileId);

            var fileOverview = FileStatusOverviewExtMapper.MapToExternalModel(file);
            return new FileStatusDetailsExt()
            {
                Checksum = fileOverview.Checksum,
                FileId = fileId,
                FileName = fileOverview.FileName,
                Sender = fileOverview.Sender,
                FileStatus = fileOverview.FileStatus,
                FileStatusChanged = fileOverview.FileStatusChanged,
                FileStatusText = fileOverview.FileStatusText,
                Metadata = fileOverview.Metadata,
                Recipients = fileOverview.Recipients,
                SendersFileReference = fileOverview.SendersFileReference,
                FileStatusHistory = FileStatusOverviewExtMapper.MapToFileStatusHistoryExt(fileHistory),
                RecipientFileStatusHistory = FileStatusOverviewExtMapper.MapToRecipients(recipientHistory, file.Sender, file.ApplicationId)
            };
        }

        /// <summary>
        /// Get files that can be accessed by the caller according to specified filters. Result set is limited to 100 files.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult<List<string>>> GetFiles()
        {
            var caller = GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }

            var files = _fileRepository.GetFilesAvailableForCaller(caller);

            return Ok(files);
        }

        /// <summary>
        /// Downloads the file
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}/download")]
        public async Task<ActionResult<Stream>> DownloadFile(Guid fileId)
        {
            var file = await _fileRepository.GetFileAsync(fileId);
            if (string.IsNullOrWhiteSpace(file?.FileLocation))
            {
                return BadRequest("No file uploaded yet");
            }

            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(file.FileLocation);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(502, "File could not be accessed at the location.");
            }
            var stream = await response.Content.ReadAsStreamAsync();
            var contentType = response.Content.Headers.ContentType.ToString();

            return File(stream, contentType, file.Filename);
        }

        /// <summary>
        /// Confirms that the file has been downloaded
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{fileId}/confirmdownload")]
        public async Task<ActionResult> ConfirmDownload(Guid fileId)
        {
            var caller = GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }

            await _fileRepository.AddReceipt(fileId, ActorFileStatus.Downloaded, caller);

            return Ok();
        }

        private string? GetCallerFromTestToken(HttpContext httpContext) => httpContext.User.Claims.FirstOrDefault(claim => claim.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}