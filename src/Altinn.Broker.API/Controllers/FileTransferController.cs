using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownloadCommand;
using Altinn.Broker.Application.DownloadFileQuery;
using Altinn.Broker.Application.GetFileTransferDetailsQuery;
using Altinn.Broker.Application.GetFileTransferOverviewQuery;
using Altinn.Broker.Application.GetFileTransfersQuery;
using Altinn.Broker.Application.InitializeFileTransferCommand;
using Altinn.Broker.Application.UploadFileCommand;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Enums;
using Altinn.Broker.Helpers;
using Altinn.Broker.Mappers;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OneOf;

namespace Altinn.Broker.Controllers
{
    [ApiController]
    [Route("broker/api/v1/filetransfer")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class FileTransferController : Controller
    {
        private readonly ILogger<FileTransferController> _logger;
        private readonly IIdempotencyEventRepository _idempotencyEventRepository;

        public FileTransferController(ILogger<FileTransferController> logger, IIdempotencyEventRepository idempotencyEventRepository)
        {
            _logger = logger;
            _idempotencyEventRepository = idempotencyEventRepository;
        }

        /// <summary>
        /// Initialize a file transfer and file upload
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Authorize(Policy = AuthorizationConstants.Sender)]
        public async Task<ActionResult<Guid>> InitializeFileTransfer(FileTransferInitalizeExt initializeExt, [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, [FromServices] InitializeFileTransferCommandHandler handler, CancellationToken cancellationToken)
        {
            LogContextHelpers.EnrichLogsWithInitializeFile(initializeExt);
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Initializing file transfer");
            var commandRequest = InitializeFileTransferMapper.MapToRequest(initializeExt, token);

            var commandResult = await handler.Process(commandRequest, cancellationToken);
            return commandResult.Match(
                fileTransferId => Ok(fileTransferId.ToString()),
                Problem
            );
        }

        /// <summary>
        /// Upload to an initialized file using a binary stream.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{fileTransferId}/upload")]
        [Consumes("application/octet-stream")]
        [Authorize(Policy = AuthorizationConstants.Sender)]
        public async Task<ActionResult> UploadUploadStreamed(
            Guid fileTransferId,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] UploadFileCommandHandler handler,
            CancellationToken cancellationToken
        )
        {
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Uploading file for file transfer {fileTransferId}", fileTransferId.ToString());
            Request.EnableBuffering();
            var commandResult = await handler.Process(new UploadFileCommandRequest()
            {
                FileTransferId = fileTransferId,
                Token = token,
                UploadStream = Request.Body
            }, cancellationToken);
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
            [FromForm] FileTransferInitializeAndUploadExt form,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] InitializeFileTransferCommandHandler initializeFileTransferCommandHandler,
            [FromServices] UploadFileCommandHandler UploadFileCommandHandler,
            CancellationToken cancellationToken
        )
        {
            LogContextHelpers.EnrichLogsWithInitializeFile(form.Metadata);
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Initializing and uploading fileTransfer");
            var initializeRequest = InitializeFileTransferMapper.MapToRequest(form.Metadata, token);
            var initializeResult = await initializeFileTransferCommandHandler.Process(initializeRequest, cancellationToken);
            if (initializeResult.IsT1)
            {
                Problem(initializeResult.AsT1);
            }
            var fileTransferId = initializeResult.AsT0;

            Request.EnableBuffering();
            var uploadResult = await UploadFileCommandHandler.Process(new UploadFileCommandRequest()
            {
                FileTransferId = fileTransferId,
                Token = token,
                UploadStream = Request.Body
            }, cancellationToken);
            return uploadResult.Match(
                FileId => Ok(FileId.ToString()),
                Problem
            );
        }

        /// <summary>
        /// Get information about the file and its current status
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileTransferId}")]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
        public async Task<ActionResult<FileTransferOverviewExt>> GetFileTransferOverview(
            Guid fileTransferId,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] GetFileTransferOverviewQueryHandler handler,
            CancellationToken cancellationToken)
        {
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Getting filetransfer overview for {fileTransferId}", fileTransferId.ToString());
            var queryResult = await handler.Process(new GetFileTransferOverviewQueryRequest()
            {
                FileTransferId = fileTransferId,
                Token = token
            }, cancellationToken);
            return queryResult.Match(
                result => Ok(FileTransferStatusOverviewExtMapper.MapToExternalModel(result.FileTransfer)),
                Problem
            );
        }

        /// <summary>
        /// Get more detailed information about the file upload for auditing and troubleshooting purposes
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("{fileTransferId}/details")]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
        public async Task<ActionResult<FileTransferStatusDetailsExt>> GetFileDetails(
            Guid fileTransferId,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] GetFileTransferDetailsQueryHandler handler,
            CancellationToken cancellationToken)
        {
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Getting fileTransfer details for {fileTransferId}", fileTransferId.ToString());
            var queryResult = await handler.Process(new GetFileTransferDetailsQueryRequest()
            {
                FileTransferId = fileTransferId,
                Token = token
            }, cancellationToken);
            return queryResult.Match(
                result => Ok(FileTransferStatusDetailsExtMapper.MapToExternalModel(result.FileTransfer, result.FileTransferEvents, result.ActorEvents)),
                Problem
            );

        }

        /// <summary>
        /// Get files that can be accessed by the caller according to specified filters. Result set is limited to 100 files.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
        public async Task<ActionResult<List<Guid>>> GetFileTransfers(
            [FromQuery] string resourceId,
            [FromQuery] FileTransferStatusExt? status,
            [FromQuery] RecipientFileTransferStatusExt? recipientStatus,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] GetFileTransfersQueryHandler handler,
            CancellationToken cancellationToken)
        {
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Getting fileTransfers with status {status} created {from} to {to}", status?.ToString(), from?.ToString(), to?.ToString());
            var queryResult = await handler.Process(new GetFileTransfersQueryRequest()
            {
                Token = token,
                ResourceId = resourceId,
                Status = status is not null ? (FileTransferStatus)status : null,
                RecipientStatus = recipientStatus is not null ? (ActorFileTransferStatus)recipientStatus : null,
                From = from,
                To = to
            }, cancellationToken);
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
        [Route("{fileTransferId}/download")]
        [Authorize(Policy = AuthorizationConstants.Recipient)]
        public async Task<ActionResult> DownloadFileTransfer(
            Guid fileTransferId,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] DownloadFileQueryHandler handler,
             CancellationToken cancellationToken)
        {
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Downloading file for file transfer {fileTransferId}", fileTransferId.ToString());
            var queryResult = await handler.Process(new DownloadFileQueryRequest()
            {
                FileTransferId = fileTransferId,
                Token = token
            }, cancellationToken);
            return queryResult.Match<ActionResult>(
                result => File(result.DownloadStream, "application/octet-stream", result.FileName),
                Problem
            );
        }

        /// <summary>
        /// Confirms that the file has been downloaded
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("{fileTransferId}/confirmdownload")]
        [Authorize(Policy = AuthorizationConstants.Recipient)]
        public async Task<ActionResult> ConfirmDownload(
            Guid fileTransferId,
            [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
            [FromServices] ConfirmDownloadCommandHandler handler,
            CancellationToken cancellationToken)
        {
            LogContextHelpers.EnrichLogsWithToken(token);
            _logger.LogInformation("Confirming download for fileTransfer {fileTransferId}", fileTransferId.ToString());
            var requestData = new ConfirmDownloadCommandRequest()
            {
                FileTransferId = fileTransferId,
                Token = token
            };
            var proccessingFunction = new Func<Task<OneOf<Task, Error>>>(() => handler.Process(requestData, cancellationToken));
            var uniqueString = $"confirmDownload_{fileTransferId}_{token.Consumer}";
            var commandResult = await IdempotencyEventHelper.ProcessEvent(uniqueString, proccessingFunction, _idempotencyEventRepository, cancellationToken);
            return commandResult.Match(
                (_) => Ok(null),
                Problem
            );
        }

        private ObjectResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
    }
}
