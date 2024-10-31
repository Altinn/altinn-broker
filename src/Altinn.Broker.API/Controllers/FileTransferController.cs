using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Models;
using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownload;
using Altinn.Broker.Application.DownloadFile;
using Altinn.Broker.Application.GetFileTransferDetails;
using Altinn.Broker.Application.GetFileTransferOverview;
using Altinn.Broker.Application.GetFileTransfers;
using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Application.UploadFile;
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

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/filetransfer")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class FileTransferController(ILogger<FileTransferController> logger, IIdempotencyEventRepository idempotencyEventRepository) : Controller
{

    /// <summary>
    /// Initialize a file transfer and file upload
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Policy = AuthorizationConstants.Sender)]
    public async Task<ActionResult<Guid>> InitializeFileTransfer(FileTransferInitalizeExt initializeExt, [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, [FromServices] InitializeFileTransferHandler handler, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
        LogContextHelpers.EnrichLogsWithInitializeFile(initializeExt);
        LogContextHelpers.EnrichLogsWithToken(token);
        logger.LogInformation("Initializing file transfer");
        var commandRequest = InitializeFileTransferMapper.MapToRequest(initializeExt, token);

        var commandResult = await handler.Process(commandRequest, cancellationToken);
        return commandResult.Match(
                fileTransferId => Ok(new FileTransferInitializeResponseExt()
                {
                    FileTransferId = fileTransferId
                }),
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
    public async Task<ActionResult> UploadStreamed(
        Guid fileTransferId,
        [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
        [FromServices] UploadFileHandler handler,
        CancellationToken cancellationToken
    )
    {
        LogContextHelpers.EnrichLogsWithToken(token);
        logger.LogInformation("Uploading file for file transfer {fileTransferId}", fileTransferId.ToString());
        Request.EnableBuffering();

        var commandResult = await handler.Process(new UploadFileRequest()
        {
            FileTransferId = fileTransferId,
            Token = token,
            UploadStream = Request.Body,
            ContentLength = Request.ContentLength ?? Request.Body.Length
        }, cancellationToken);
        return commandResult.Match(
            fileTransferId => Ok(new FileTransferUploadResponseExt()
            {
                FileTransferId = fileTransferId
            }),
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
        [FromServices] InitializeFileTransferHandler initializeFileTransferHandler,
        [FromServices] UploadFileHandler UploadFileHandler,
        CancellationToken cancellationToken
    )
    {
        LogContextHelpers.EnrichLogsWithInitializeFile(form.Metadata);
        LogContextHelpers.EnrichLogsWithToken(token);
        logger.LogInformation("Initializing and uploading fileTransfer");
        var initializeRequest = InitializeFileTransferMapper.MapToRequest(form.Metadata, token);
        var initializeResult = await initializeFileTransferHandler.Process(initializeRequest, cancellationToken);
        if (initializeResult.IsT1)
        {
            Problem(initializeResult.AsT1);
        }
        var fileTransferId = initializeResult.AsT0;

        Request.EnableBuffering();
        var uploadResult = await UploadFileHandler.Process(new UploadFileRequest()
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
        [FromServices] GetFileTransferOverviewHandler handler,
        CancellationToken cancellationToken)
    {
        LogContextHelpers.EnrichLogsWithToken(token);
        logger.LogInformation("Getting filetransfer overview for {fileTransferId}", fileTransferId.ToString());
        var queryResult = await handler.Process(new GetFileTransferOverviewRequest()
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
    public async Task<ActionResult<FileTransferStatusDetailsExt>> GetFileTransferDetails(
        Guid fileTransferId,
        [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
        [FromServices] GetFileTransferDetailsHandler handler,
        CancellationToken cancellationToken)
    {
        LogContextHelpers.EnrichLogsWithToken(token);
        logger.LogInformation("Getting fileTransfer details for {fileTransferId}", fileTransferId.ToString());
        var queryResult = await handler.Process(new GetFileTransferDetailsRequest()
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
    /// Get files that can be accessed by the caller according to specified filters. Result set is limited to 100 files. If your query returns more than 100 files, you will only receive the 100 last created.
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
        [FromServices] GetFileTransfersHandler handler,
        CancellationToken cancellationToken)
    {
        LogContextHelpers.EnrichLogsWithToken(token);
        logger.LogInformation("Getting fileTransfers with status {status} created {from} to {to}", status?.ToString(), from?.ToString(), to?.ToString());
        var queryResult = await handler.Process(new GetFileTransfersRequest()
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
    public async Task<ActionResult> DownloadFile(
        Guid fileTransferId,
        [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
        [FromServices] DownloadFileHandler handler,
         CancellationToken cancellationToken)
    {
        LogContextHelpers.EnrichLogsWithToken(token);
        logger.LogInformation("Downloading file for file transfer {fileTransferId}", fileTransferId.ToString());
        var queryResult = await handler.Process(new DownloadFileRequest()
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
        [FromServices] ConfirmDownloadHandler handler,
        CancellationToken cancellationToken)
    {
        LogContextHelpers.EnrichLogsWithToken(token);
        logger.LogInformation("Confirming download for fileTransfer {fileTransferId}", fileTransferId.ToString());
        var requestData = new ConfirmDownloadRequest()
        {
            FileTransferId = fileTransferId,
            Token = token
        };
        var proccessingFunction = new Func<Task<OneOf<Task, Error>>>(() => handler.Process(requestData, cancellationToken));
        var uniqueString = $"confirmDownload_{fileTransferId}_{token.Consumer}";
        var commandResult = await IdempotencyEventHelper.ProcessEvent(uniqueString, proccessingFunction, idempotencyEventRepository, cancellationToken);
        return commandResult.Match(
            (_) => Ok(null),
            Problem
        );
    }

    private ObjectResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
}
