using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Helpers;
using Altinn.Broker.Mappers;
using Altinn.Broker.Models;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers
{
    [ApiController]
    [Route("broker/api/v1/file")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class FileController : Controller
    {
        private readonly IFileRepository _fileRepository;
        private readonly IServiceOwnerRepository _serviceOwnerRepository;
        private readonly IBrokerStorageService _brokerStorageService;
        private readonly IResourceManager _resourceManager;
        private readonly ILogger<FileController> _logger;

        public FileController(IFileRepository fileRepository, IServiceOwnerRepository serviceOwnerRepository, IBrokerStorageService brokerStorageService, IResourceManager resourceManager, ILogger<FileController> logger)
        {
            _fileRepository = fileRepository;
            _serviceOwnerRepository = serviceOwnerRepository;
            _brokerStorageService = brokerStorageService;
            _resourceManager = resourceManager;
            _logger = logger;
        }

        /// <summary>
        /// Initialize a file upload
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<Guid>> InitializeFile(FileInitalizeExt initializeExt)
        {
            (ServiceOwnerEntity? serviceOwner, ObjectResult? authenticationResult) = await AuthenticationSimulator.AuthenticateRequestAsync(HttpContext, _serviceOwnerRepository);
            if (authenticationResult is not null)
            {
                return authenticationResult;
            }
            if (serviceOwner is null)
            {
                return Unauthorized();
            }

            var file = FileInitializeExtMapper.MapToDomain(initializeExt, serviceOwner.Id);
            var fileId = await _fileRepository.AddFileAsync(file, serviceOwner);

            return Ok(fileId.ToString());
        }

        /// <summary>
        /// Upload to an initialized file using a binary stream.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{fileId}/upload")]
        [Consumes("application/octet-stream")]
        public async Task<ActionResult> UploadFileStreamed(
            Guid fileId)
        {
            (ServiceOwnerEntity? serviceOwner, ObjectResult? authenticationResult) = await AuthenticationSimulator.AuthenticateRequestAsync(HttpContext, _serviceOwnerRepository);
            if (authenticationResult is not null)
            {
                return authenticationResult;
            }
            if (serviceOwner is null || serviceOwner.StorageProvider is null)
            {
                return Unauthorized();
            }

            var file = await _fileRepository.GetFileAsync(fileId);
            if (file is null)
            {
                return BadRequest();
            }
            Request.EnableBuffering();

            await _fileRepository.InsertFileStatus(fileId, FileStatus.UploadStarted);
            await _brokerStorageService.UploadFile(serviceOwner, file, Request.Body);
            await _fileRepository.SetStorageReference(fileId, serviceOwner.StorageProvider.Id, fileId.ToString());
            // TODO: Queue Kafka jobs
            await _fileRepository.InsertFileStatus(fileId, FileStatus.UploadProcessing);
            await _fileRepository.InsertFileStatus(fileId, FileStatus.Published);

            return Ok(fileId.ToString());
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
            (ServiceOwnerEntity? serviceOwner, ObjectResult? authenticationResult) = await AuthenticationSimulator.AuthenticateRequestAsync(HttpContext, _serviceOwnerRepository);
            if (authenticationResult is not null)
            {
                return authenticationResult;
            }
            if (serviceOwner is null || serviceOwner.StorageProvider is null)
            {
                return Unauthorized();
            }

            var file = FileInitializeExtMapper.MapToDomain(form.Metadata, serviceOwner.Id);
            var fileId = await _fileRepository.AddFileAsync(file, serviceOwner);
            await _fileRepository.InsertFileStatus(fileId, FileStatus.UploadStarted);
            await _brokerStorageService.UploadFile(serviceOwner, file, form.File.OpenReadStream());
            await _fileRepository.SetStorageReference(fileId, serviceOwner.StorageProvider.Id, fileId.ToString());
            // TODO: Queue Kafka jobs
            await _fileRepository.InsertFileStatus(fileId, FileStatus.UploadProcessing);
            await _fileRepository.InsertFileStatus(fileId, FileStatus.Published);
            return Ok(fileId.ToString());
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
            (ServiceOwnerEntity? serviceOwner, ObjectResult? authenticationResult) = await AuthenticationSimulator.AuthenticateRequestAsync(HttpContext, _serviceOwnerRepository);
            if (authenticationResult is not null)
            {
                return authenticationResult;
            }
            if (serviceOwner is null)
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
            var caller = AuthenticationSimulator.GetCallerFromTestToken(HttpContext) ?? "politiet";
            var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(caller);
            if (serviceOwner is not null)
            {
                var deploymentStatus = await _resourceManager.GetDeploymentStatus(serviceOwner);
                if (deploymentStatus != DeploymentStatus.Ready)
                {
                    return UnprocessableEntity($"Service owner infrastructure is not ready. Status is: ${nameof(deploymentStatus)}");
                }
            }
            if (serviceOwner is null)
            {
                return Unauthorized();
            }

            var file = await _fileRepository.GetFileAsync(fileId);
            if (file is null)
            {
                return NotFound();
            }
            if (string.IsNullOrWhiteSpace(file?.FileLocation))
            {
                return BadRequest("No file uploaded yet");
            }

            var downloadStream = await _brokerStorageService.DownloadFile(serviceOwner, file);
            await _fileRepository.AddReceipt(fileId, ActorFileStatus.DownloadStarted, caller);

            return File(downloadStream, "application/force-download", file.Filename);
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
