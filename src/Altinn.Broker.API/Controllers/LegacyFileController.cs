using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;
using Altinn.Broker.Application.ConfirmDownloadCommand;
using Altinn.Broker.Application.DownloadFileQuery;
using Altinn.Broker.Application.GetFileTransferOverviewQuery;
using Altinn.Broker.Application.GetFileTransfersQuery;
using Altinn.Broker.Application.InitializeFileTransferCommand;
using Altinn.Broker.Application.UploadFileCommand;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Enums;
using Altinn.Broker.Helpers;
using Altinn.Broker.Mappers;
using Altinn.Broker.Middlewares;
using Altinn.Broker.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

/// <summary>
/// The LegacyFileController allows integration from the Altinn 2 BrokerBridge component to allow legacy users access to Altinn 3 Broker
/// </summary>
[ApiController]
[Route("broker/api/legacy/v1/file")]
[Authorize(AuthenticationSchemes = AuthorizationConstants.Legacy)]
[Authorize(Policy = AuthorizationConstants.Legacy)]
public class LegacyFileController : Controller
{
    private readonly ILogger<LegacyFileController> _logger;

    public LegacyFileController(ILogger<LegacyFileController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initialize a file upload
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<Guid>> InitializeFile(LegacyFileInitalizeExt initializeExt, [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token, [FromServices] InitializeFileTransferCommandHandler handler, CancellationToken cancellationToken)
    {
        CallerIdentity legacyToken = CreateLegacyToken(initializeExt.Sender, token);

        LogContextHelpers.EnrichLogsWithLegacyInitializeFile(initializeExt);
        LogContextHelpers.EnrichLogsWithToken(legacyToken);
        _logger.LogInformation("Legacy - Initializing file");
        var commandRequest = LegacyInitializeFileMapper.MapToRequest(initializeExt, token);
        var commandResult = await handler.Process(commandRequest, cancellationToken);
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
        [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
        [FromServices] UploadFileCommandHandler handler,
        CancellationToken cancellationToken
    )
    {
        CallerIdentity legacyToken = CreateLegacyToken(onBehalfOfConsumer, token);

        LogContextHelpers.EnrichLogsWithToken(legacyToken);
        _logger.LogInformation("Legacy - Uploading file for file transfer {fileId}", fileTransferId.ToString());
        Request.EnableBuffering();
        var commandResult = await handler.Process(new UploadFileCommandRequest()
        {
            FileTransferId = fileTransferId,
            Token = legacyToken,
            UploadStream = Request.Body,
            IsLegacy = true
        }, cancellationToken);
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
        [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
        [FromServices] GetFileTransferOverviewQueryHandler handler,
        CancellationToken cancellationToken)
    {
        CallerIdentity legacyToken = CreateLegacyToken(onBehalfOfConsumer, token);

        LogContextHelpers.EnrichLogsWithToken(legacyToken);
        _logger.LogInformation("Legacy - Getting file overview for {fileId}", fileId.ToString());
        var queryResult = await handler.Process(new GetFileTransferOverviewQueryRequest()
        {
            FileTransferId = fileId,
            Token = legacyToken,
            IsLegacy = true
        }, cancellationToken);
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
        [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
        [FromServices] LegacyGetFilesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        // HasAvailableFiles calls are not made on behalf of any consumer.
        CallerIdentity? legacyToken = null;
        if (!string.IsNullOrWhiteSpace(onBehalfOfConsumer))
        {
            legacyToken = CreateLegacyToken(onBehalfOfConsumer, token);
        }

        LogContextHelpers.EnrichLogsWithToken(legacyToken ?? token);
        string recipientsString = string.Empty;
        if (recipients?.Length > 0)
        {
            recipientsString = string.Join(',', recipients);
            _logger.LogInformation("Getting files with status {status} created {from} to {to} for recipients {recipients}", recipientStatus?.ToString(), from?.ToString(), to?.ToString(), recipientsString);
        }
        else
        {
            _logger.LogInformation("Getting files with status {status} created {from} to {to} for consumer {consumer}", recipientStatus?.ToString(), from?.ToString(), to?.ToString(), onBehalfOfConsumer);
        }

        var queryResult = await handler.Process(new LegacyGetFilesQueryRequest()
        {
            Token = legacyToken ?? token,
            ResourceId = resourceId ?? string.Empty,
            RecipientFileTransferStatus = recipientStatus is not null ? (ActorFileTransferStatus)recipientStatus : null,
            FileTransferStatus = status is not null ? (FileTransferStatus)status : null,
            OnBehalfOfConsumer = onBehalfOfConsumer,
            From = from,
            To = to,
            Recipients = recipients
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
    [Route("{fileId}/download")]
    public async Task<ActionResult> DownloadFile(
         Guid fileId,
        [FromQuery] string onBehalfOfConsumer,
        [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
        [FromServices] DownloadFileQueryHandler handler,
        CancellationToken cancellationToken)
    {
        CallerIdentity? legacyToken = CreateLegacyToken(onBehalfOfConsumer, token);
        LogContextHelpers.EnrichLogsWithToken(legacyToken);
        _logger.LogInformation("Downloading file {fileId}", fileId.ToString());
        var queryResult = await handler.Process(new DownloadFileQueryRequest()
        {
            FileTransferId = fileId,
            Token = legacyToken,
            IsLegacy = true
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
    [Route("{fileId}/confirmdownload")]
    public async Task<ActionResult> ConfirmDownload(
        Guid fileId,
        [FromQuery] string onBehalfOfConsumer,
        [ModelBinder(typeof(MaskinportenModelBinder))] CallerIdentity token,
        [FromServices] ConfirmDownloadCommandHandler handler,
         CancellationToken cancellationToken)
    {
        CallerIdentity? legacyToken = CreateLegacyToken(onBehalfOfConsumer, token);
        LogContextHelpers.EnrichLogsWithToken(legacyToken);
        _logger.LogInformation("Confirming download for file {fileId}", fileId.ToString());
        var commandResult = await handler.Process(new ConfirmDownloadCommandRequest()
        {
            FileTransferId = fileId,
            Token = legacyToken,
            IsLegacy = true
        }, cancellationToken);
        return commandResult.Match(
            Ok,
            Problem
        );
    }

    private ObjectResult Problem(Error error) => Problem(detail: error.Message, statusCode: (int)error.StatusCode);

    private static CallerIdentity CreateLegacyToken(string onBehalfOfConsumer, CallerIdentity callingToken)
    {
        return new CallerIdentity(callingToken.Scope, onBehalfOfConsumer, callingToken.ClientId);
    }
}
