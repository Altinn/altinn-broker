using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.Application;
using Altinn.Broker.Application.CheckNotificationCondition;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("broker/api/v1/notification")]
[ApiExplorerSettings(IgnoreApi = true)]
public class NotificationController(ILogger<NotificationController> logger) : Controller
{
    /// <summary>
    /// Tells Altinn Notification whether a notification or reminder should still be sent. Called by them, not by our
    /// API users, right before sending.
    /// </summary>
    [HttpGet]
    [Route("{notificationId}/check")]
    [Authorize(Policy = AuthorizationConstants.NotificationCheck)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CheckNotificationConditionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult> CheckNotificationCondition(
        Guid notificationId,
        [FromServices] CheckNotificationConditionHandler handler,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Checking notification condition for notification {NotificationId}", notificationId);
        var commandResult = await handler.Process(notificationId, HttpContext.User, cancellationToken);
        return commandResult.Match<ActionResult>(
            response => Ok(response),
            Problem
        );
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}
