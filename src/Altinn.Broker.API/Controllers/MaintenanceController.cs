using Altinn.Broker.Application;
using Altinn.Broker.API.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Altinn.Broker.Application.CleanupUseCaseTests;

namespace Altinn.Broker.API.Controllers;


[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("broker/api/v1/maintenance")]
[Authorize]

public class MaintenanceController(ILogger<MaintenanceController> logger) : Controller
{
    private readonly ILogger<MaintenanceController> _logger = logger;

    /// <summary>
    /// Cleanup test data (dialogs and correspondences) for a given resourceId used by use case tests
    /// </summary>
    /// <response code="200">Returns a summary of deleted correspondences</response>
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
        [FromServices] CleanupUseCaseTestsHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to cleanup use case test data received");
        var result = await handler.Process(HttpContext.User, cancellationToken);
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