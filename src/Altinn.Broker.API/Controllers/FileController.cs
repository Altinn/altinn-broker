using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownloadCommand;
using Altinn.Broker.Application.DownloadFileQuery;
using Altinn.Broker.Application.GetFileDetailsQuery;
using Altinn.Broker.Application.GetFileOverviewQuery;
using Altinn.Broker.Application.GetFilesQuery;
using Altinn.Broker.Application.InitializeFileCommand;
using Altinn.Broker.Application.UploadFileCommand;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Helpers;
using Altinn.Broker.Mappers;
using Altinn.Broker.Middlewares;
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
        private readonly ILogger<FileController> _logger;

        public FileController(ILogger<FileController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initialize a file upload
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Authorize(Policy = AuthorizationConstants.Sender)]
        public async Task<ActionResult<Guid>> InitializeFile(FileInitalizeExt initializeExt, [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, [FromServices] InitializeFileCommandHandler handler)
        {
            LogContextHelpers.EnrichLogsWithInitializeFile(initializeExt);
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Initializing file");
            var commandRequest = InitializeFileMapper.MapToRequest(initializeExt, token);
            var commandResult = await handler.Process(commandRequest);
            return commandResult.Match(
                fileId => Ok(fileId.ToString()),
                Problem
            );
        }

        /// <summary>
        /// Upload to an initialized file using a binary stream.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{fileId}/upload")]
        [Consumes("application/octet-stream")]
        [Authorize(Policy = AuthorizationConstants.Sender)]
        public async Task<ActionResult> UploadFileStreamed(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] UploadFileCommandHandler handler
        )
        {
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Uploading file {fileId}", fileId.ToString());
            Request.EnableBuffering();
            var commandResult = await handler.Process(new UploadFileCommandRequest()
            {
                FileId = fileId,
                Token = token,
                Filestream = Request.Body
            });
            return commandResult.Match(
                fileId => Ok(fileId.ToString()),
                Problem
            );
        }

        /// <summary>
        /// Initialize and upload a file using form-data
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("upload")]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        [Authorize(Policy = AuthorizationConstants.Sender)]
        public async Task<ActionResult> InitializeAndUpload(
            [FromForm] FileInitializeAndUploadExt form,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] InitializeFileCommandHandler initializeFileCommandHandler,
            [FromServices] UploadFileCommandHandler uploadFileCommandHandler
        )
        {
            LogContextHelpers.EnrichLogsWithInitializeFile(form.Metadata);
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Initializing and uploading file");
            var initializeRequest = InitializeFileMapper.MapToRequest(form.Metadata, token);
            var initializeResult = await initializeFileCommandHandler.Process(initializeRequest);
            if (initializeResult.IsT1)
            {
                Problem(initializeResult.AsT1);
            }
            var fileId = initializeResult.AsT0;

            Request.EnableBuffering();
            var uploadResult = await uploadFileCommandHandler.Process(new UploadFileCommandRequest()
            {
                FileId = fileId,
                Token = token,
                Filestream = Request.Body
            });
            return uploadResult.Match(
                fileId => Ok(fileId.ToString()),
                Problem
            );
        }

        /// <summary>
        /// Get information about the file and its current status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}")]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
        public async Task<ActionResult<FileOverviewExt>> GetFileOverview(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] GetFileOverviewQueryHandler handler)
        {
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Getting file overview for {fileId}", fileId.ToString());
            var queryResult = await handler.Process(new GetFileOverviewQueryRequest()
            {
                FileId = fileId,
                Token = token
            });
            return queryResult.Match(
                result => Ok(FileStatusOverviewExtMapper.MapToExternalModel(result.File)),
                Problem
            );
        }

        /// <summary>
        /// Get more detailed information about the file upload for auditing and troubleshooting purposes
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}/details")]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
        public async Task<ActionResult<FileStatusDetailsExt>> GetFileDetails(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] GetFileDetailsQueryHandler handler)
        {
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Getting file details for {fileId}", fileId.ToString());
            var queryResult = await handler.Process(new GetFileDetailsQueryRequest()
            {
                FileId = fileId,
                Token = token
            });
            return queryResult.Match(
                result => Ok(FileStatusDetailsExtMapper.MapToExternalModel(result.File, result.FileEvents, result.ActorEvents)),
                Problem
            );
        }

        /// <summary>
        /// Get files that can be accessed by the caller according to specified filters. Result set is limited to 100 files.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
        public async Task<ActionResult<List<Guid>>> GetFiles(
            [FromQuery] string resourceId,
            [FromQuery] FileStatusExt? status,
            [FromQuery] RecipientFileStatusExt? recipientStatus,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] GetFilesQueryHandler handler)
        {
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Getting files with status {status} created {from} to {to}", status?.ToString(), from?.ToString(), to?.ToString());
            var queryResult = await handler.Process(new GetFilesQueryRequest()
            {
                Token = token,
                ResourceId = resourceId,
                Status = status is not null ? (FileStatus)status : null,
                RecipientStatus = recipientStatus is not null ? (ActorFileStatus)recipientStatus : null,
                From = from,
                To = to
            });
            return queryResult.Match(
                Ok,
                Problem
            );
        }

        /// <summary>
        /// Downloads the file
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}/download")]
        [Authorize(Policy = AuthorizationConstants.Recipient)]
        public async Task<ActionResult> DownloadFile(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] DownloadFileQueryHandler handler)
        {
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Downloading file {fileId}", fileId.ToString());
            var queryResult = await handler.Process(new DownloadFileQueryRequest()
            {
                FileId = fileId,
                Token = token
            });
            return queryResult.Match<ActionResult>(
                result => File(result.Stream, "application/octet-stream", result.Filename),
                Problem
            );
        }

        /// <summary>
        /// Confirms that the file has been downloaded
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{fileId}/confirmdownload")]
        [Authorize(Policy = AuthorizationConstants.Recipient)]
        public async Task<ActionResult> ConfirmDownload(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] ConfirmDownloadCommandHandler handler)
        {
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Confirming download for file {fileId}", fileId.ToString());
            var commandResult = await handler.Process(new ConfirmDownloadCommandRequest()
            {
                FileId = fileId,
                Token = token
            });
            return commandResult.Match(
                (_) => Ok(null),
                Problem
            );
        }

        private ObjectResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
    }
}
