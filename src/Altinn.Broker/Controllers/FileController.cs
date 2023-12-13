using Altinn.Broker.Application.ConfirmDownloadCommand;
using Altinn.Broker.Application.DownloadFileQuery;
using Altinn.Broker.Application.GetFileDetailsQuery;
using Altinn.Broker.Application.GetFileOverviewQuery;
using Altinn.Broker.Application.GetFilesQuery;
using Altinn.Broker.Application.InitializeFileCommand;
using Altinn.Broker.Application.UploadFileCommand;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Helpers;
using Altinn.Broker.Mappers;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models;
using Altinn.Broker.Models.Maskinporten;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Serilog.Context;

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
        [Authorize(Policy = "Sender")]
        public async Task<ActionResult<Guid>> InitializeFile(FileInitalizeExt initializeExt, [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token, [FromServices] InitializeFileCommandHandler handler)
        {
            LogContextHelpers.EnrichLogsWithInitializeFile(initializeExt);
            LogContextHelpers.EnrichLogsWithMaskinporten(token);
            _logger.LogInformation("Initializing file");
            var commandRequest = InitializeFileMapper.MapToRequest(initializeExt, token);
            var commandResult = await handler.Process(commandRequest);
            return commandResult.Match(
                fileId => Ok(fileId.ToString()),
                error => error.ToActionResult()
            );
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
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] UploadFileCommandHandler handler
        )
        {
            LogContextHelpers.EnrichLogsWithMaskinporten(token);
            _logger.LogInformation("Uploading file {fileId}", fileId.ToString());
            Request.EnableBuffering();
            var commandResult = await handler.Process(new UploadFileCommandRequest()
            {
                FileId = fileId,
                Consumer = token.Consumer,
                Supplier = token.Supplier,
                Filestream = Request.Body
            });
            return commandResult.Match(
                fileId => Ok(fileId.ToString()),
                error => error.ToActionResult()
            );
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
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] InitializeFileCommandHandler initializeFileCommandHandler,
            [FromServices] UploadFileCommandHandler uploadFileCommandHandler
        )
        {
            LogContextHelpers.EnrichLogsWithInitializeFile(form.Metadata);
            LogContextHelpers.EnrichLogsWithMaskinporten(token);
            _logger.LogInformation("Initializing and uploading file");
            var initializeRequest = InitializeFileMapper.MapToRequest(form.Metadata, token);
            var initializeResult = await initializeFileCommandHandler.Process(initializeRequest);
            if (initializeResult.IsT1)
            {
                return initializeResult.AsT1.ToActionResult();
            }
            var fileId = initializeResult.AsT0;

            Request.EnableBuffering();
            var uploadResult = await uploadFileCommandHandler.Process(new UploadFileCommandRequest()
            {
                FileId = fileId,
                Supplier = token.Supplier,
                Consumer = token.Consumer,
                Filestream = Request.Body
            });
            return uploadResult.Match(
                fileId => Ok(fileId.ToString()),
                error => error.ToActionResult()
            );
        }

        /// <summary>
        /// Get information about the file and its current status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}")]
        [Authorize(Policy = "Sender")]
        public async Task<ActionResult<FileOverviewExt>> GetFileOverview(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] GetFileOverviewQueryHandler handler)
        {
            LogContextHelpers.EnrichLogsWithMaskinporten(token);
            _logger.LogInformation("Getting file overview for {fileId}", fileId.ToString());
            var queryResult = await handler.Process(new GetFileOverviewQueryRequest()
            {
                FileId = fileId,
                Consumer = token.Consumer,
                Supplier = token.Supplier
            });
            return queryResult.Match(
                result => Ok(FileStatusOverviewExtMapper.MapToExternalModel(result.File)),
                error => error.ToActionResult()
            );
        }

        /// <summary>
        /// Get more detailed information about the file upload for auditing and troubleshooting purposes
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}/details")]
        [Authorize(Policy = "Sender")]
        public async Task<ActionResult<FileStatusDetailsExt>> GetFileDetails(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] GetFileDetailsQueryHandler handler)
        {
            LogContextHelpers.EnrichLogsWithMaskinporten(token);
            _logger.LogInformation("Getting file details for {fileId}", fileId.ToString());
            var queryResult = await handler.Process(new GetFileDetailsQueryRequest()
            {
                FileId = fileId,
                Consumer = token.Consumer,
                Supplier = token.Supplier
            });
            return queryResult.Match(
                result => Ok(FileStatusDetailsExtMapper.MapToExternalModel(result.File, result.FileEvents, result.ActorEvents)),
                error => error.ToActionResult()
            );
        }

        /// <summary>
        /// Get files that can be accessed by the caller according to specified filters. Result set is limited to 100 files.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Policy = "Sender")]
        public async Task<ActionResult<List<Guid>>> GetFiles(
            [FromQuery] FileStatusExt? status,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] GetFilesQueryHandler handler)
        {
            LogContextHelpers.EnrichLogsWithMaskinporten(token);
            _logger.LogInformation("Getting files with status {status} created {from} to {to}", status?.ToString(), from?.ToString(), to?.ToString());
            var queryResult = await handler.Process(new GetFilesQueryRequest()
            {
                Consumer = token.Consumer,
                Supplier = token.Supplier,
                Status = status is not null ? (FileStatus)status : null,
                From = from,
                To = to
            });
            return queryResult.Match(
                Ok,
                error => error.ToActionResult()
            );
        }

        /// <summary>
        /// Downloads the file
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileId}/download")]
        [Authorize(Policy = "Recipient")]
        public async Task<ActionResult<Stream>> DownloadFile(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] DownloadFileQueryHandler handler)
        {
            LogContextHelpers.EnrichLogsWithMaskinporten(token);
            _logger.LogInformation("Downloading file {fileId}", fileId.ToString());
            var queryResult = await handler.Process(new DownloadFileQueryRequest()
            {
                FileId = fileId,
                Consumer = token.Consumer,
                Supplier = token.Supplier
            });
            return queryResult.Match(
                result => File(result.Stream, "application/octet-stream", result.Filename),
                error => error.ToActionResult()
            );
        }

        /// <summary>
        /// Confirms that the file has been downloaded
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{fileId}/confirmdownload")]
        [Authorize(Policy = "Recipient")]
        public async Task<ActionResult> ConfirmDownload(
            Guid fileId,
            [ModelBinder(typeof(MaskinportenModelBinder))] MaskinportenToken token,
            [FromServices] ConfirmDownloadCommandHandler handler)
        {
            LogContextHelpers.EnrichLogsWithMaskinporten(token);
            _logger.LogInformation("Confirming download for file {fileId}", fileId.ToString());
            var commandResult = await handler.Process(new ConfirmDownloadCommandRequest()
            {
                FileId = fileId,
                Supplier = token.Supplier,
                Consumer = token.Consumer
            });
            return commandResult.Match(
                Ok,
                error => error.ToActionResult()
            );
        }
    }
}
