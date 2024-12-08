using System.Text.RegularExpressions;

using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownload;
using Altinn.Broker.Application.DownloadFile;
using Altinn.Broker.Application.GetFileTransferOverview;
using Altinn.Broker.Application.GetFileTransfers;
using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Application.UploadFile;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Enums;
using Altinn.Broker.Helpers;
using Altinn.Broker.Mappers;
using Altinn.Broker.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

/// <summary>
/// The LegacyFileController allows integration from the Altinn 2 BrokerBridge component to allow legacy users access to Altinn 3 Broker
/// </summary>
[ApiController]
[Route("broker/api/v1/legacy/file")]
[Authorize(AuthenticationSchemes = AuthorizationConstants.Legacy)]
[Authorize(Policy = AuthorizationConstants.Legacy)]
public class LegacyFileController(ILogger<LegacyFileController> logger) : Controller
{

    /// <summary>
    /// Initialize a file upload
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<Guid>> InitializeFile(LegacyFileInitalizeExt initializeExt, [FromServices] InitializeFileTransferHandler handler, CancellationToken cancellationToken)
    {
        LogContextHelpers.EnrichLogsWithLegacyInitializeFile(initializeExt);
        logger.LogInformation("Legacy - Initializing file");
        var commandRequest = LegacyInitializeFileMapper.MapToRequest(initializeExt);
        var commandResult = await handler.Process(commandRequest, HttpContext.User, cancellationToken);
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
    [Route("{fileTransferId}/upload")]
    [Consumes("application/octet-stream")]
    public async Task<ActionResult> UploadFileStreamed(
        Guid fileTransferId,
        [FromQuery] string onBehalfOfConsumer,
        [FromServices] UploadFileHandler handler,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Legacy - Uploading file for file transfer {fileId}", fileTransferId.ToString());
        if (Request.ContentLength is null)
        {
            return Problem("Content-length header is required");
        }
        var commandResult = await handler.Process(new UploadFileRequest()
        {
            FileTransferId = fileTransferId,
            UploadStream = Request.Body,
            ContentLength = Request.ContentLength.Value,
            IsLegacy = true,
            OnBehalfOfConsumer = onBehalfOfConsumer
        }, HttpContext.User, cancellationToken);
        return commandResult.Match(
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
    public async Task<ActionResult<LegacyFileOverviewExt>> GetFileOverview(
        Guid fileId,
        [FromQuery] string onBehalfOfConsumer,
        [FromServices] GetFileTransferOverviewHandler handler,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Legacy - Getting file overview for {fileId}", fileId.ToString());
        var queryResult = await handler.Process(new GetFileTransferOverviewRequest()
        {
            FileTransferId = fileId,
            IsLegacy = true,
            OnBehalfOfConsumer = onBehalfOfConsumer
        }, HttpContext.User, cancellationToken);
        return queryResult.Match(
            result => Ok(LegacyFileStatusOverviewExtMapper.MapToExternalModel(result.FileTransfer)),
            Problem
        );
    }

    /// <summary>
    /// Get files that can be accessed by the caller according to specified filters.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult<List<Guid>>> GetFiles(
        [FromQuery] FileTransferStatusExt? status,
        [FromQuery] RecipientFileTransferStatusExt? recipientStatus,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? resourceId,
        [FromQuery] string? onBehalfOfConsumer,
        [FromQuery] string[]? recipients,
        [FromServices] LegacyGetFilesHandler handler,
        CancellationToken cancellationToken)
    {
        // HasAvailableFiles calls are not made on behalf of any consumer.
        var organizationNumberPattern = new Regex(Constants.OrgNumberPattern);
        if (recipients?.Length > 0)
        {
            var recipientsString = string.Join(',', recipients);
            logger.LogInformation("Getting files with status {status} created {from} to {to} for recipients {recipients}", recipientStatus?.ToString(), from?.ToString(), to?.ToString(), recipientsString.SanitizeForLogs());
        }
        else
        {
            logger.LogInformation("Getting files with status {status} created {from} to {to} for consumer {consumer}", recipientStatus?.ToString(), from?.ToString(), to?.ToString(), onBehalfOfConsumer?.SanitizeForLogs());
        }

        var queryResult = await handler.Process(new LegacyGetFilesRequest()
        {
            ResourceId = resourceId ?? string.Empty,
            RecipientFileTransferStatus = recipientStatus is not null ? (ActorFileTransferStatus)recipientStatus : null,
            FileTransferStatus = status is not null ? (FileTransferStatus)status : null,
            OnBehalfOfConsumer = onBehalfOfConsumer,
            From = from,
            To = to,
            Recipients = recipients
        }, HttpContext.User, cancellationToken);
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
    public async Task<ActionResult> DownloadFile(
         Guid fileId,
        [FromQuery] string onBehalfOfConsumer,
        [FromServices] DownloadFileHandler handler,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Downloading file {fileId}", fileId.ToString());
        var queryResult = await handler.Process(new DownloadFileRequest()
        {
            FileTransferId = fileId,
            IsLegacy = true,
            OnBehalfOf = onBehalfOfConsumer
        }, HttpContext.User, cancellationToken);
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
    [Route("{fileId}/confirmdownload")]
    public async Task<ActionResult> ConfirmDownload(
        Guid fileId,
        [FromQuery] string onBehalfOfConsumer,
        [FromServices] ConfirmDownloadHandler handler,
         CancellationToken cancellationToken)
    {
        logger.LogInformation("Confirming download for file {fileId}", fileId.ToString());
        var commandResult = await handler.Process(new ConfirmDownloadRequest()
        {
            FileTransferId = fileId,
            IsLegacy = true,
            OnBehalfOfConsumer = onBehalfOfConsumer
        }, HttpContext.User, cancellationToken);
        return commandResult.Match(
            Ok,
            Problem
        );
    }

    private ObjectResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);
}
