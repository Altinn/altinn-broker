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
using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Enums;
using Altinn.Broker.Helpers;
using Altinn.Broker.Mappers;
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
    /// Initialize a file transfer
    /// </summary>
    /// <remarks>
    /// Scopes: <br />
    /// - altinn:broker.write
    /// </remarks>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Policy = AuthorizationConstants.Sender)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(FileTransferInitializeResponseExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<Guid>> InitializeFileTransfer(FileTransferInitalizeExt initializeExt, [FromServices] InitializeFileTransferHandler handler, CancellationToken cancellationToken)
    {
        LogContextHelpers.EnrichLogsWithInitializeFile(initializeExt);
        logger.LogInformation("Initializing file transfer");
        var commandRequest = InitializeFileTransferMapper.MapToRequest(initializeExt);
        var commandResult = await handler.Process(commandRequest, HttpContext.User, cancellationToken);
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
    /// <remarks>
    /// Scopes: <br />
    /// - altinn:broker.write
    /// </remarks>
    /// <returns></returns>
    [HttpPost]
    [Route("{fileTransferId}/upload")]
    [Consumes("application/octet-stream")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(FileTransferUploadResponseExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [Authorize(Policy = AuthorizationConstants.Sender)]
    public async Task<ActionResult> UploadStreamed(
        Guid fileTransferId,
        [FromServices] UploadFileHandler handler,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Uploading file for file transfer {fileTransferId}", fileTransferId.ToString());

        if (Request.ContentLength is null)
        {
            return Problem("Content-length header is required");
        }
        var commandResult = await handler.Process(new UploadFileRequest()
        {
            FileTransferId = fileTransferId,
            UploadStream = Request.Body,
            ContentLength = Request.ContentLength.Value
        }, HttpContext.User, cancellationToken);
        return commandResult.Match(
            fileTransferId => Ok(new FileTransferUploadResponseExt()
            {
                FileTransferId = fileTransferId
            }),
            Problem
        );
    }

    /// <summary>
    /// Initialize a filetransfer and uploads the file in the same request using form-data
    /// </summary>
    /// <remarks>
    /// Scopes: <br />
    /// - altinn:broker.write
    /// </remarks>
    /// <returns></returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [Route("upload")]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    [Authorize(Policy = AuthorizationConstants.Sender)]
    public async Task<ActionResult> InitializeAndUpload(
        [FromForm] FileTransferInitializeAndUploadExt form,
        [FromServices] InitializeFileTransferHandler initializeFileTransferHandler,
        [FromServices] UploadFileHandler UploadFileHandler,
        CancellationToken cancellationToken
    )
    {
        LogContextHelpers.EnrichLogsWithInitializeFile(form.Metadata);
        logger.LogInformation("Initializing and uploading fileTransfer");
        var initializeRequest = InitializeFileTransferMapper.MapToRequest(form.Metadata);
        var initializeResult = await initializeFileTransferHandler.Process(initializeRequest, HttpContext.User, cancellationToken);
        if (initializeResult.IsT1)
        {
            Problem(initializeResult.AsT1);
        }
        var fileTransferId = initializeResult.AsT0;

        Request.EnableBuffering();
        var uploadResult = await UploadFileHandler.Process(new UploadFileRequest()
        {
            FileTransferId = fileTransferId,
            UploadStream = Request.Body
        }, HttpContext.User, cancellationToken);
        return uploadResult.Match(
            FileId => Ok(FileId.ToString()),
            Problem
        );
    }

    /// <summary>
    /// Get information about the file transfer and its current status
    /// </summary>
    /// <remarks>
    /// Scopes: <br />
    /// - altinn:broker.read <br/>
    /// - altinn:broker.write
    /// </remarks>
    /// <returns></returns>
    [HttpGet]
    [Route("{fileTransferId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(FileTransferOverviewExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
    public async Task<ActionResult<FileTransferOverviewExt>> GetFileTransferOverview(
        Guid fileTransferId,
        [FromServices] GetFileTransferOverviewHandler handler,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting filetransfer overview for {fileTransferId}", fileTransferId.ToString());
        var queryResult = await handler.Process(new GetFileTransferOverviewRequest()
        {
            FileTransferId = fileTransferId
        }, HttpContext.User, cancellationToken);
        return queryResult.Match(
            result => Ok(FileTransferStatusOverviewExtMapper.MapToExternalModel(result.FileTransfer)),
            Problem
        );
    }

    /// <summary>
    /// Get more detailed information about the file transfer for auditing and troubleshooting purposes
    /// </summary>
    /// <remarks>
    /// Scopes: <br />
    /// - altinn:broker.read <br/>
    /// - altinn:broker.write
    /// </remarks>
    /// <returns></returns>
    [HttpGet]
    [Route("{fileTransferId}/details")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(FileTransferStatusDetailsExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
    public async Task<ActionResult<FileTransferStatusDetailsExt>> GetFileTransferDetails(
        Guid fileTransferId,
        [FromServices] GetFileTransferDetailsHandler handler,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting fileTransfer details for {fileTransferId}", fileTransferId.ToString());
        var queryResult = await handler.Process(new GetFileTransferDetailsRequest()
        {
            FileTransferId = fileTransferId
        }, HttpContext.User, cancellationToken);
        return queryResult.Match(
            result => Ok(FileTransferStatusDetailsExtMapper.MapToExternalModel(result.FileTransfer, result.FileTransferEvents, result.ActorEvents)),
            Problem
        );

    }

    /// <summary>
    /// Get files that can be accessed by the caller according to specified filters
    /// </summary>
    /// <remarks>
    /// Scopes: <br />
    /// - altinn:broker.read <br/>
    /// - altinn:broker.write <br/>
    /// Result is limited to 100 files. If your query returns more than 100 files, you will only receive the 100 last created.
    /// </remarks>
    /// <returns></returns>
    [HttpGet]
    [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<Guid>>> GetFileTransfers(
        [FromQuery] string resourceId,
        [FromQuery] FileTransferStatusExt? status,
        [FromQuery] RecipientFileTransferStatusExt? recipientStatus,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromServices] GetFileTransfersHandler handler,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting fileTransfers with status {status} created {from} to {to}", status?.ToString(), from?.ToString(), to?.ToString());
        var queryResult = await handler.Process(new GetFileTransfersRequest()
        {
            ResourceId = resourceId,
            Status = status is not null ? (FileTransferStatus)status : null,
            RecipientStatus = recipientStatus is not null ? (ActorFileTransferStatus)recipientStatus : null,
            From = from,
            To = to
        }, HttpContext.User, cancellationToken);
        return queryResult.Match(
            Ok,
            Problem
        );
    }

    /// <summary>
    /// Downloads the file
    /// </summary>
    /// <remarks>
    /// Scopes: <br />
    /// - altinn:broker.read <br/>
    /// </remarks>
    /// <returns></returns>
    [HttpGet]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Route("{fileTransferId}/download")]
    [Authorize(Policy = AuthorizationConstants.Recipient)]
    public async Task<ActionResult> DownloadFile(
        Guid fileTransferId,
        [FromServices] DownloadFileHandler handler,
         CancellationToken cancellationToken)
    {
        logger.LogInformation("Downloading file for file transfer {fileTransferId}", fileTransferId.ToString());
        var queryResult = await handler.Process(new DownloadFileRequest()
        {
            FileTransferId = fileTransferId
        }, HttpContext.User, cancellationToken);
        return queryResult.Match<ActionResult>(
            result => File(result.DownloadStream, "application/octet-stream", result.FileName),
            Problem
        );
    }

    /// <summary>
    /// Confirms that the file has been downloaded
    /// </summary>
    /// <remarks>
    /// Scopes: <br/> 
    /// - altinn:broker.read <br/>
    /// </remarks>
    /// <returns></returns>
    [HttpPost]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Route("{fileTransferId}/confirmdownload")]
    [Authorize(Policy = AuthorizationConstants.Recipient)]
    public async Task<ActionResult> ConfirmDownload(
        Guid fileTransferId,
        [FromServices] ConfirmDownloadHandler handler,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Confirming download for fileTransfer {fileTransferId}", fileTransferId.ToString());
        var requestData = new ConfirmDownloadRequest()
        {
            FileTransferId = fileTransferId
        };
        var proccessingFunction = new Func<Task<OneOf<Task, Error>>>(() => handler.Process(requestData, HttpContext.User, cancellationToken));
        var uniqueString = $"confirmDownload_{fileTransferId}_{HttpContext.User.GetCallerOrganizationId()}";
        var commandResult = await IdempotencyEventHelper.ProcessEvent(uniqueString, proccessingFunction, idempotencyEventRepository, cancellationToken);
        return commandResult.Match(
            (_) => Ok(null),
            Problem
        );
    }

    private ObjectResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);

}
