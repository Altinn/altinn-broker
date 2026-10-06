using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.Application;
using Altinn.Broker.Application.GetActiveFileTransferDetails;
using Altinn.Broker.Application.GetFileTransferSummaries;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Mappers;
using Altinn.Broker.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/frontend")]
public class FrontendController(ILogger<FrontendController> logger) : Controller
{
    /// <summary>
    /// Get a lean summary of active (published) file transfers across multiple resources in a single call
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br />
    /// - altinn:broker.read <br/>
    /// - altinn:broker.write <br/>
    /// A resourceId the caller can't access (or that doesn't exist) is skipped rather than failing the whole call.
    /// Returns one page, newest first. <c>hasNextPage</c> says whether more exist, and
    /// <c>continuationToken</c> is passed back to read the next page. <c>search</c> matches the
    /// sender's reference and is ignored below three characters. <c>role</c> limits the list to
    /// what the party sent or received.
    /// </remarks>
    /// <response code="200">Returns the list of active file transfer summaries</response>
    /// <response code="401">You must use a bearer token that represents a system user with access to the resource in the Resource Rights Registry</response>
    [HttpGet("active-file-transfers")]
    [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(FileTransferSummaryListExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FileTransferSummaryListExt>> GetActiveFileTransfers(
        [FromQuery] List<string> resourceIds,
        [FromQuery] string? onBehalfOf,
        [FromServices] GetFileTransferSummariesHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] string? continuationToken = null,
        [FromQuery] string? search = null,
        [FromQuery] SearchRole role = SearchRole.Both)
    {
        logger.LogInformation("Getting active file transfers for {count} resources", resourceIds.Count);
        var queryResult = await handler.Process(new GetFileTransferSummariesRequest()
        {
            ResourceIds = resourceIds,
            OnBehalfOf = onBehalfOf ?? string.Empty,
            View = FileTransferListView.Active,
            ContinuationToken = continuationToken,
            Search = search,
            Role = role
        }, HttpContext.User, cancellationToken);
        return queryResult.Match(
            page => Ok(FileTransferSummaryExtMapper.MapToExternalModel(page)),
            Problem
        );
    }

    /// <summary>
    /// Get a lean summary of historical (past Published - cancelled, purged, failed, or fully
    /// confirmed downloaded) file transfers across multiple resources in a single call
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br />
    /// - altinn:broker.read <br/>
    /// - altinn:broker.write <br/>
    /// A resourceId the caller can't access (or that doesn't exist) is skipped rather than failing the whole call.
    /// Returns one page, newest first. <c>hasNextPage</c> says whether more exist, and
    /// <c>continuationToken</c> is passed back to read the next page. <c>search</c> matches the
    /// sender's reference and is ignored below three characters. <c>role</c> limits the list to
    /// what the party sent or received.
    /// </remarks>
    /// <response code="200">Returns the list of historical file transfer summaries</response>
    /// <response code="401">You must use a bearer token that represents a system user with access to the resource in the Resource Rights Registry</response>
    [HttpGet("historical-file-transfers")]
    [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(FileTransferSummaryListExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FileTransferSummaryListExt>> GetHistoricalFileTransfers(
        [FromQuery] List<string> resourceIds,
        [FromQuery] string? onBehalfOf,
        [FromServices] GetFileTransferSummariesHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] string? continuationToken = null,
        [FromQuery] string? search = null,
        [FromQuery] SearchRole role = SearchRole.Both)
    {
        logger.LogInformation("Getting historical file transfers for {count} resources", resourceIds.Count);
        var queryResult = await handler.Process(new GetFileTransferSummariesRequest()
        {
            ResourceIds = resourceIds,
            OnBehalfOf = onBehalfOf ?? string.Empty,
            View = FileTransferListView.Historical,
            ContinuationToken = continuationToken,
            Search = search,
            Role = role
        }, HttpContext.User, cancellationToken);
        return queryResult.Match(
            page => Ok(FileTransferSummaryExtMapper.MapToExternalModel(page)),
            Problem
        );
    }

    [HttpGet("file-transfer-details/{fileTransferId}")]
    [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(GetActiveFileTransferDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GetActiveFileTransferDetailsResponse>> GetActiveFileTransferDetails(
        [FromRoute] Guid fileTransferId,
        [FromQuery] string? onBehalfOf,
        [FromServices] GetActiveFileTransferDetailsHandler handler,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting active file transfer details for {fileTransferId}", fileTransferId);
        var queryResult = await handler.Process(new GetActiveFileTransferDetailsRequest()
        {
            FileTransferId = fileTransferId,
            OnBehalfOf = onBehalfOf ?? string.Empty
        }, HttpContext.User, cancellationToken);
        return queryResult.Match(
            details => Ok(details),
            Problem
        );
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}
