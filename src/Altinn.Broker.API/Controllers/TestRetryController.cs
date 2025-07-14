using Altinn.Broker.Application.TestRetry;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Controllers;

[ApiController]
[Route("api/test-retry")]
[ApiExplorerSettings(IgnoreApi = true)]
public class TestRetryController : ControllerBase
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<TestRetryController> _logger;

    public TestRetryController(
        IBackgroundJobClient backgroundJobClient,
        ILogger<TestRetryController> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a background job that will fail immediately to test Slack retry filtering
    /// </summary>
    /// <returns>Job ID of the failed job</returns>
    [HttpPost("trigger-failing-job")]
    public ActionResult<string> TriggerFailingJob()
    {
        var jobId = _backgroundJobClient.Enqueue<TestRetryHandler>(handler => 
            handler.ProcessFailingJob());
        
        _logger.LogInformation("Triggered failing job with ID: {JobId}", jobId);
        
        return Ok(new { 
            JobId = jobId, 
            Message = "Failing job enqueued. Check Hangfire dashboard and Slack for notifications." 
        });
    }

    /// <summary>
    /// Triggers a background job that will fail after a delay to test Slack retry filtering
    /// </summary>
    /// <param name="delaySeconds">Delay in seconds before the job fails (default: 5)</param>
    /// <returns>Job ID of the delayed failing job</returns>
    [HttpPost("trigger-delayed-failing-job")]
    public ActionResult<string> TriggerDelayedFailingJob([FromQuery] int delaySeconds = 5)
    {
        var jobId = _backgroundJobClient.Enqueue<TestRetryHandler>(handler => 
            handler.ProcessDelayedFailingJob(delaySeconds));
        
        _logger.LogInformation("Triggered delayed failing job with ID: {JobId}, delay: {DelaySeconds}s", jobId, delaySeconds);
        
        return Ok(new { 
            JobId = jobId, 
            Message = $"Delayed failing job enqueued with {delaySeconds}s delay. Check Hangfire dashboard and Slack for notifications.",
            DelaySeconds = delaySeconds
        });
    }

    /// <summary>
    /// Triggers a recurring job that will fail to test Slack retry filtering
    /// </summary>
    /// <returns>Success message</returns>
    [HttpPost("trigger-recurring-failing-job")]
    public ActionResult<string> TriggerRecurringFailingJob()
    {
        var recurringJobId = "test-recurring-failing-job";
        
        RecurringJob.AddOrUpdate<TestRetryHandler>(
            recurringJobId,
            handler => handler.ProcessFailingJob(),
            "*/2 * * * *"); // Run every 2 minutes
        
        _logger.LogInformation("Triggered recurring failing job with ID: {RecurringJobId}", recurringJobId);
        
        return Ok(new { 
            RecurringJobId = recurringJobId, 
            Message = "Recurring failing job scheduled (every 2 minutes). Check Hangfire dashboard and Slack for notifications.",
            Schedule = "*/2 * * * *"
        });
    }

    /// <summary>
    /// Removes the recurring failing job
    /// </summary>
    /// <returns>Success message</returns>
    [HttpDelete("remove-recurring-failing-job")]
    public ActionResult<string> RemoveRecurringFailingJob()
    {
        var recurringJobId = "test-recurring-failing-job";
        RecurringJob.RemoveIfExists(recurringJobId);
        
        _logger.LogInformation("Removed recurring failing job with ID: {RecurringJobId}", recurringJobId);
        
        return Ok(new { 
            RecurringJobId = recurringJobId, 
            Message = "Recurring failing job removed."
        });
    }
} 