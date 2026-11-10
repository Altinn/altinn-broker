using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.Application;
using Altinn.Broker.Application.GetActiveFileTransferDetails;
using Altinn.Broker.Application.GetFileTransferSummaries;
using Altinn.Broker.Application.UploadPlans;
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
    /// <c>continuationToken</c> is passed back to read the next page.
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
        [FromQuery] string? continuationToken = null)
    {
        logger.LogInformation("Getting active file transfers for {count} resources", resourceIds.Count);
        var queryResult = await handler.Process(new GetFileTransferSummariesRequest()
        {
            ResourceIds = resourceIds,
            OnBehalfOf = onBehalfOf ?? string.Empty,
            View = FileTransferListView.Active,
            ContinuationToken = continuationToken
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
    /// <c>continuationToken</c> is passed back to read the next page.
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
        [FromQuery] string? continuationToken = null)
    {
        logger.LogInformation("Getting historical file transfers for {count} resources", resourceIds.Count);
        var queryResult = await handler.Process(new GetFileTransferSummariesRequest()
        {
            ResourceIds = resourceIds,
            OnBehalfOf = onBehalfOf ?? string.Empty,
            View = FileTransferListView.Historical,
            ContinuationToken = continuationToken
        }, HttpContext.User, cancellationToken);
        return queryResult.Match(
            page => Ok(FileTransferSummaryExtMapper.MapToExternalModel(page)),
            Problem
        );
    }

    /// <summary>
    /// Get a lean summary of the caller's draft file transfers - its own transfers whose file has not
    /// finished uploading - across multiple resources in a single call
    /// </summary>
    /// <remarks>
    /// One of the scopes: <br />
    /// - altinn:broker.write <br/>
    /// A resourceId the caller can't access (or that doesn't exist) is skipped rather than failing the whole call.
    /// Returns one page, newest first. <c>hasNextPage</c> says whether more exist, and
    /// <c>continuationToken</c> is passed back to read the next page.
    /// </remarks>
    /// <response code="200">Returns the list of draft file transfer summaries</response>
    /// <response code="401">You must use a bearer token that represents a system user with access to the resource in the Resource Rights Registry</response>
    [HttpGet("draft-file-transfers")]
    [Authorize(Policy = AuthorizationConstants.Sender)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(FileTransferSummaryListExt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FileTransferSummaryListExt>> GetDraftFileTransfers(
        [FromQuery] List<string> resourceIds,
        [FromQuery] string? onBehalfOf,
        [FromServices] GetFileTransferSummariesHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] string? continuationToken = null)
    {
        logger.LogInformation("Getting draft file transfers for {count} resources", resourceIds.Count);
        var queryResult = await handler.Process(new GetFileTransferSummariesRequest()
        {
            ResourceIds = resourceIds,
            OnBehalfOf = onBehalfOf ?? string.Empty,
            View = FileTransferListView.Drafts,
            ContinuationToken = continuationToken
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

    /// <summary>
    /// Save how the file of a transfer is split into tus uploads, so the upload can be continued from another browser
    /// </summary>
    /// <remarks>
    /// The parts must cover the file from the first byte to the last, and each must be a tus upload already
    /// created for the transfer. A later call replaces the plan. The file transfer id in the body is ignored.
    /// </remarks>
    /// <response code="204">The plan was saved</response>
    /// <response code="400">The plan does not match the file transfer or its uploads</response>
    /// <response code="409">The file transfer no longer takes uploads</response>
    [HttpPut("file-transfers/{fileTransferId}/upload-plan")]
    [Authorize(Policy = AuthorizationConstants.Sender)]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SaveUploadPlan(
        [FromRoute] Guid fileTransferId,
        [FromBody] SaveUploadPlanRequest request,
        [FromServices] SaveUploadPlanHandler handler,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Saving upload plan for {fileTransferId}", fileTransferId);
        request.FileTransferId = fileTransferId;
        var commandResult = await handler.Process(request, HttpContext.User, cancellationToken);
        return commandResult.Match(
            _ => NoContent(),
            Problem
        );
    }

    /// <summary>
    /// Get the plan for continuing the upload of a transfer's file, with how much of each part the server holds
    /// </summary>
    /// <response code="200">Returns the upload plan</response>
    /// <response code="404">The file transfer has no upload that can be continued</response>
    [HttpGet("file-transfers/{fileTransferId}/upload-plan")]
    [Authorize(Policy = AuthorizationConstants.Sender)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(GetUploadPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetUploadPlanResponse>> GetUploadPlan(
        [FromRoute] Guid fileTransferId,
        [FromServices] GetUploadPlanHandler handler,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting upload plan for {fileTransferId}", fileTransferId);
        var queryResult = await handler.Process(new GetUploadPlanRequest()
        {
            FileTransferId = fileTransferId
        }, HttpContext.User, cancellationToken);
        return queryResult.Match(
            plan => Ok(plan),
            Problem
        );
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}
