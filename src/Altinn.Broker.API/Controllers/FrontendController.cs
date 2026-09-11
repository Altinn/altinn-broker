using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.Application;
using Altinn.Broker.Application.GetActiveFileTransfers;
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
    /// </remarks>
    /// <response code="200">Returns the list of active file transfer summaries</response>
    /// <response code="401">You must use a bearer token that represents a system user with access to the resource in the Resource Rights Registry</response>
    [HttpGet("active-file-transfers")]
    [ApiExplorerSettings(IgnoreApi = false)]
    [Authorize(Policy = AuthorizationConstants.SenderOrRecipient)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<ActiveFileTransferExt>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ActiveFileTransferExt>>> GetActiveFileTransfers(
        [FromQuery] List<string> resourceIds,
        [FromQuery] string? onBehalfOf,
        [FromServices] GetActiveFileTransfersHandler handler,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting active file transfers for {count} resources", resourceIds.Count);
        var queryResult = await handler.Process(new GetActiveFileTransfersRequest()
        {
            ResourceIds = resourceIds,
            OnBehalfOf = onBehalfOf ?? string.Empty
        }, HttpContext.User, cancellationToken);
        return queryResult.Match(
            summaries => Ok(summaries.Select(ActiveFileTransferExtMapper.MapToExternalModel).ToList()),
            Problem
        );
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}
