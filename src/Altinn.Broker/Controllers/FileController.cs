using Altinn.Broker.Application.InitializeFileCommand;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Mappers;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models;
using Altinn.Broker.Models.Maskinporten;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

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
        [Authorize(Policy = "Sender")]
        public async Task<ActionResult<Guid>> InitializeFile(FileInitalizeExt initializeExt, [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token)
        {
            if (token.Consumer != initializeExt.Sender)
            {
                return Unauthorized("You must use a bearer token that belongs to the sender");
            }
            var file = FileInitializeExtMapper.MapToDomain(initializeExt, token.Supplier);
            var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Supplier);

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
        [Authorize(Policy = "Sender")]
        public async Task<ActionResult> UploadFileStreamed(
            Guid fileId, [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token)
        {
            var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Supplier);
            if (serviceOwner is null)
            {
                return Unauthorized("Service owner not configured for the broker service");
            };
            var deploymentStatus = await _resourceManager.GetDeploymentStatus(serviceOwner);
            if (deploymentStatus != DeploymentStatus.Ready)
            {
                return UnprocessableEntity($"Service owner infrastructure is not ready. Status is: ${nameof(deploymentStatus)}");
            }
            var file = await _fileRepository.GetFileAsync(fileId);
            if (file is null)
            {
                return BadRequest();
            }
            if (token.Consumer != file.Sender)
            {
                return Unauthorized("You must use a bearer token that belongs to the sender");
            }

            Request.EnableBuffering();
            await _fileRepository.InsertFileStatus(fileId, FileStatus.UploadStarted);
            await _brokerStorageService.UploadFile(serviceOwner, file, Request.Body);
            await _fileRepository.SetStorageReference(fileId, serviceOwner.StorageProvider.Id, fileId.ToString());
            // TODO, async jobs
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
        [Authorize(Policy = "Sender")]
        public async Task<ActionResult> InitializeAndUpload(
            [FromForm] FileInitializeAndUploadExt form,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token
        )
        {
            if (token.Consumer != form.Metadata.Sender)
            {
                return Unauthorized("You must use a bearer token that belongs to the sender");
            }
            var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Supplier);
            if (serviceOwner is null)
            {
                return Unauthorized("Service owner not configured for the broker service");
            };
            var deploymentStatus = await _resourceManager.GetDeploymentStatus(serviceOwner);
            if (deploymentStatus != DeploymentStatus.Ready)
            {
                return UnprocessableEntity($"Service owner infrastructure is not ready. Status is: ${nameof(deploymentStatus)}");
            }

            var file = FileInitializeExtMapper.MapToDomain(form.Metadata, token.Supplier);
            var fileId = await _fileRepository.AddFileAsync(file, serviceOwner);
            await _fileRepository.InsertFileStatus(fileId, FileStatus.UploadStarted);
            await _brokerStorageService.UploadFile(serviceOwner, file, form.File.OpenReadStream());
            await _fileRepository.SetStorageReference(fileId, serviceOwner.StorageProvider.Id, fileId.ToString());
            // TODO, async jobs
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
        [Authorize(Policy = "Sender")]
        public async Task<ActionResult<FileOverviewExt>> GetFileStatus(Guid fileId, [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token)
        {
            var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Supplier);
            if (serviceOwner is null)
            {
                return Unauthorized("Service owner not configured for the broker service");
            };
            var file = await _fileRepository.GetFileAsync(fileId);
            if (file is null)
            {
                return NotFound();
            }
            if (!file.ActorEvents.Any(actorEvent => actorEvent.Actor.ActorExternalId == token.Consumer))
            {
                return NotFound();
            }

            var fileView = FileStatusOverviewExtMapper.MapToExternalModel(file);

            return Ok(fileView);
        }

        /// <summary>
        /// Get more detailed information about the file upload for auditing and troubleshooting purposes
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}/details")]
        [Authorize(Policy = "Sender")]
        public async Task<ActionResult<FileStatusDetailsExt>> GetFileDetails(Guid fileId, [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token)
        {
            var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Supplier);
            if (serviceOwner is null)
            {
                return Unauthorized("Service owner not configured for the broker service");
            };
            var file = await _fileRepository.GetFileAsync(fileId);
            if (file is null)
            {
                return NotFound();
            }
            if (!file.ActorEvents.Any(actorEvent => actorEvent.Actor.ActorExternalId == token.Consumer))
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
        [Authorize(Policy = "Sender")]
        public async Task<ActionResult<List<Guid>>> GetFiles([FromQuery] FileStatus? status, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token)
        {
            var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Supplier);
            if (serviceOwner is null)
            {
                return Unauthorized("Service owner not configured for the broker service");
            };

            var files = await _fileRepository.GetFilesAvailableForCaller(token.Consumer);

            return Ok(files);
        }

        /// <summary>
        /// Downloads the file
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}/download")]
        [Authorize(Policy = "Recipient")]
        public async Task<ActionResult<Stream>> DownloadFile(Guid fileId, [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token)
        {
            var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Supplier);
            if (serviceOwner is null)
            {
                return Unauthorized("Service owner not configured for the broker service");
            };
            var file = await _fileRepository.GetFileAsync(fileId);
            if (file is null)
            {
                return NotFound();
            }
            if (!file.ActorEvents.Any(actorEvent => actorEvent.Actor.ActorExternalId == token.Consumer))
            {
                return NotFound();
            }
            if (string.IsNullOrWhiteSpace(file?.FileLocation))
            {
                return BadRequest("No file uploaded yet");
            }

            var downloadStream = await _brokerStorageService.DownloadFile(serviceOwner, file);
            await _fileRepository.AddReceipt(fileId, ActorFileStatus.DownloadStarted, token.Consumer);

            return File(downloadStream, "application/force-download", file.Filename);
        }

        /// <summary>
        /// Confirms that the file has been downloaded
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{fileId}/confirmdownload")]
        [Authorize(Policy = "Recipient")]
        public async Task<ActionResult> ConfirmDownload(Guid fileId, [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token)
        {
            var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(token.Supplier);
            if (serviceOwner is null)
            {
                return Unauthorized("Service owner not configured for the broker service");
            };
            var file = await _fileRepository.GetFileAsync(fileId);
            if (file is null)
            {
                return NotFound();
            }
            if (!file.ActorEvents.Any(actorEvent => actorEvent.Actor.ActorExternalId == token.Consumer))
            {
                return NotFound();
            }
            if (string.IsNullOrWhiteSpace(file?.FileLocation))
            {
                return BadRequest("No file uploaded yet");
            }

            await _fileRepository.AddReceipt(fileId, ActorFileStatus.DownloadConfirmed, token.Consumer);
            var recipientStatuses = file.ActorEvents
                .Where(actorEvent => actorEvent.Actor.ActorExternalId != file.Sender && actorEvent.Actor.ActorExternalId != token.Consumer)
                .GroupBy(actorEvent => actorEvent.Actor.ActorExternalId)
                .Select(group => group.Max(statusEvent => statusEvent.Status))
                .ToList();
            bool shouldConfirmAll = recipientStatuses.All(status => status >= ActorFileStatus.DownloadConfirmed);
            await _fileRepository.InsertFileStatus(fileId, FileStatus.AllConfirmedDownloaded);

            return Ok();
        }
    }
}
