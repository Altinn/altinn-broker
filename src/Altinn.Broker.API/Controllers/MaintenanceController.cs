using Altinn.Broker.Application;
using Altinn.Broker.API.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Altinn.Broker.Application.CleanupUseCaseTests;
using Altinn.Broker.Core.Helpers;

namespace Altinn.Broker.API.Controllers;


[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("broker/api/v1/maintenance")]
[Authorize]

public class MaintenanceController(ILogger<MaintenanceController> logger) : Controller
{
    private readonly ILogger<MaintenanceController> _logger = logger;

    /// <summary>
    /// Cleanup test data for a given testTag used by use case tests
    /// </summary>
    /// <param name="testTag">The test tag to identify which test's data to clean up (e.g., 'useCaseTestsA3' or 'useCaseTestsLegacy')</param>
    /// <response code="200">Returns a summary of deleted file transfers</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost]
    [Route("cleanup-usecasetests")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CleanupUseCaseTestsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> CleanupUseCaseTestsData(
        [FromQuery] string testTag,
        [FromServices] CleanupUseCaseTestsHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to cleanup use case test data received for testTag: {TestTag}", testTag.SanitizeForLogs());
        var result = await handler.Process(new CleanupUseCaseTestsRequest { TestTag = testTag }, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    private ActionResult Problem(Error error) => Problem(
        detail: error.Message,
        statusCode: (int)error.StatusCode,
        extensions: new Dictionary<string, object?> { { "errorCode", error.ErrorCode } });
}