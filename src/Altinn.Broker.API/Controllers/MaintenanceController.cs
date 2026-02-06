using Altinn.Broker.Application;
using Altinn.Broker.API.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Altinn.Broker.Application.CleanupUseCaseTests;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.API.Helpers;

using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.API.Controllers;


[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("broker/api/v1/maintenance")]
[Authorize]

public class MaintenanceController(ILogger<MaintenanceController> logger) : Controller
{
    private readonly ILogger<MaintenanceController> _logger = logger;

    /// <summary>
    /// Cleanup use case test data for the broker resource.
    /// Optionally scopes cleanup to data older than a given age in days.
    /// </summary>
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
        [FromQuery] int? minAgeDays,
        [FromServices] CleanupUseCaseTestsHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request to cleanup use case test data received");

        var request = new CleanupUseCaseTestsRequest
        {
            MinAgeDays = minAgeDays
        };

        var result = await handler.Process(request, HttpContext.User, cancellationToken);
        return result.Match(
            Ok,
            Problem
        );
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}