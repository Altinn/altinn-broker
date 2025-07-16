using Altinn.Broker.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Broker.API.Controllers;

[ApiController]
[Route("test")]
public class TestController(ILogger<TestController> logger) : Controller
{
    /// <summary>
    /// Test endpoint to verify Slack notification for stuck file transfers
    /// </summary>
    /// <remarks>
    /// This endpoint is for testing purposes only and should be removed in production.
    /// It triggers a test Slack notification to verify the notification system is working.
    /// </remarks>
    /// <response code="200">Test notification sent successfully</response>
    /// <response code="500">Failed to send test notification</response>
    [HttpPost]
    [Route("slack-notification")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> TestSlackNotification(
        [FromServices] SlackStuckFileTransferNotifier slackNotifier,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Testing Slack notification for stuck file transfers");
        
        try
        {
            // Create a test FileTransferStatusEntity
            var testStatus = new FileTransferStatusEntity
            {
                FileTransferId = Guid.NewGuid(),
                Status = FileTransferStatus.UploadProcessing,
                Date = DateTime.UtcNow.AddMinutes(-20), // Simulate a file stuck for 20 minutes
                DetailedStatus = "Test notification - file stuck in UploadProcessing"
            };
            
            var success = await slackNotifier.NotifyFileStuckWithStatus(testStatus);
            
            if (success)
            {
                logger.LogInformation("Test Slack notification sent successfully");
                return Ok(new { message = "Test notification sent successfully", fileTransferId = testStatus.FileTransferId });
            }
            else
            {
                logger.LogError("Failed to send test Slack notification");
                return StatusCode(500, new { message = "Failed to send test notification" });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while sending test Slack notification");
            return StatusCode(500, new { message = "Exception occurred while sending test notification", error = ex.Message });
        }
    }
} 