using Altinn.Broker.Application;
using Altinn.Broker.API.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Altinn.Broker.Application.CleanupUseCaseTests;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.Application.MaskinportenJwkRotation;
using Hangfire;

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

    [HttpPost]
    [Route("maskinporten-jwk-rotation")]
    [Authorize(Policy = AuthorizationConstants.Maintenance)]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult TriggerMaskinportenJwkRotation(
        [FromServices] IBackgroundJobClient backgroundJobClient,
        [FromBody] TriggerMaskinportenJwkRotationRequest request)
    {
        const string expectedConfirmation = "rotate-maskinporten-jwk";
        if (!string.Equals(request.Confirmation, expectedConfirmation, StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = $"Confirmation must be '{expectedConfirmation}' to trigger Maskinporten JWK rotation."
            });
        }

        var jobId = backgroundJobClient.Enqueue<MaskinportenJwkRotationHandler>(
            handler => handler.Process(CancellationToken.None));

        _logger.LogWarning(
            "Manual Maskinporten JWK rotation job {JobId} was enqueued by {User}.",
            jobId,
            HttpContext.User.Identity?.Name ?? "unknown");

        return Ok(new
        {
            jobId,
            message = "Maskinporten JWK rotation was enqueued."
        });
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}

public class TriggerMaskinportenJwkRotationRequest
{
    public string Confirmation { get; set; } = string.Empty;
}
