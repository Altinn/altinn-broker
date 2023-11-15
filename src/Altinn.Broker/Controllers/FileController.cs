using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Helpers;
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
        private readonly IServiceOwnerRepository _serviceOwnerRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FileController> _logger;

        public FileController(IFileRepository fileRepository, IFileStore fileStore, IServiceOwnerRepository serviceOwnerRepository, IHttpClientFactory httpClientFactory, ILogger<FileController> logger)
        {
            _fileRepository = fileRepository;
            _fileStore = fileStore;
            _serviceOwnerRepository = serviceOwnerRepository;
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
            var caller = AuthenticationSimulator.GetCallerFromTestToken(HttpContext);
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
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<ActionResult> UploadFileStreamed(
            Guid fileId)
        {
            _logger.LogInformation("File size is {FileSize]} bytes", Request.ContentLength);
            var caller = AuthenticationSimulator.GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }
            var file = await _fileRepository.GetFileAsync(fileId);
            if (file is null)
            {
                return BadRequest();
            }
            var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(caller);

            await _fileRepository.InsertFileStatus(fileId, FileStatus.UploadStarted);
            await _fileStore.UploadFile(Request.Body, fileId, serviceOwner?.StorageAccountConnectionString);
            await _fileRepository.SetStorageReference(fileId, "altinn-3-" + fileId.ToString());
            // TODO: Queue Kafka jobs
            await _fileRepository.InsertFileStatus(fileId, FileStatus.UploadProcessing);
            await _fileRepository.InsertFileStatus(fileId, FileStatus.Published);

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
            var caller = AuthenticationSimulator.GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }
            var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(caller);

            var file = FileInitializeExtMapper.MapToDomain(form.Metadata, caller);
            var fileId = await _fileRepository.AddFileAsync(file, caller);
            await _fileRepository.InsertFileStatus(fileId, FileStatus.UploadStarted);
            await _fileStore.UploadFile(form.File.OpenReadStream(), fileId, serviceOwner?.StorageAccountConnectionString);
            await _fileRepository.SetStorageReference(fileId, "altinn-3-" + fileId.ToString());
            // TODO: Queue Kafka jobs
            await _fileRepository.InsertFileStatus(fileId, FileStatus.UploadProcessing);
            await _fileRepository.InsertFileStatus(fileId, FileStatus.Published);
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
            var caller = AuthenticationSimulator.GetCallerFromTestToken(HttpContext);
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
            var actorEvents = await _fileRepository.GetActorEvents(fileId);

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
                PropertyList = fileOverview.PropertyList,
                Recipients = fileOverview.Recipients,
                SendersFileReference = fileOverview.SendersFileReference,
                FileStatusHistory = FileStatusOverviewExtMapper.MapToFileStatusHistoryExt(fileHistory),
                RecipientFileStatusHistory = FileStatusOverviewExtMapper.MapToRecipientEvents(actorEvents.Where(actorEvents => actorEvents.Actor.ActorExternalId != file.Sender).ToList())
            };
        }

        /// <summary>
        /// Get files that can be accessed by the caller according to specified filters. Result set is limited to 100 files.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult<List<Guid>>> GetFiles([FromQuery] FileStatus? status, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
        {
            var caller = AuthenticationSimulator.GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }

            var files = await _fileRepository.GetFilesAvailableForCaller(caller);

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
            if (file is null)
            {
                return NotFound();
            }
            if (string.IsNullOrWhiteSpace(file?.FileLocation))
            {
                return BadRequest("No file uploaded yet");
            }
            var caller = AuthenticationSimulator.GetCallerFromTestToken(HttpContext);
            var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(caller);

            if (file.FileLocation.StartsWith("altinn-3"))
            {
                var stream = await _fileStore.GetFileStream(fileId, serviceOwner?.StorageAccountConnectionString);
                return File(stream, "application/octet-stream", file.Filename);
            }
            else
            {
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
        }

        /// <summary>
        /// Confirms that the file has been downloaded
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{fileId}/confirmdownload")]
        public async Task<ActionResult> ConfirmDownload(Guid fileId)
        {
            var caller = AuthenticationSimulator.GetCallerFromTestToken(HttpContext);
            if (string.IsNullOrWhiteSpace(caller))
            {
                return Unauthorized();
            }

            var file = await _fileRepository.GetFileAsync(fileId);
            if (file is null)
            {
                return NotFound();
            }

            await _fileRepository.AddReceipt(fileId, ActorFileStatus.DownloadConfirmed, caller);
            var recipientStatuses = file.ActorEvents
                .Where(actorEvent => actorEvent.Actor.ActorExternalId != file.Sender && actorEvent.Actor.ActorExternalId != caller)
                .GroupBy(actorEvent => actorEvent.Actor.ActorExternalId)
                .Select(group => group.Max(statusEvent => statusEvent.Status))
                .ToList();
            bool shouldConfirmAll = recipientStatuses.All(status => status >= ActorFileStatus.DownloadConfirmed);
            await _fileRepository.InsertFileStatus(fileId, FileStatus.AllConfirmedDownloaded);

            return Ok();
        }
    }
}
