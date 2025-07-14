using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.TestRetry;

public class TestRetryHandler
{
    private readonly ILogger<TestRetryHandler> _logger;

    public TestRetryHandler(ILogger<TestRetryHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Processes a job that fails immediately to test Slack retry filtering
    /// </summary>
    [AutomaticRetry(Attempts = 10)]
    public void ProcessFailingJob()
    {
        _logger.LogInformation("Processing failing job - this will throw an exception to test retry filtering");
        
        // Simulate some work
        Thread.Sleep(100);
        
        throw new InvalidOperationException("This is a test exception to verify Slack retry filtering functionality. This job is designed to fail.");
    }

    /// <summary>
    /// Processes a job that fails after a delay to test Slack retry filtering
    /// </summary>
    /// <param name="delaySeconds">Delay in seconds before the job fails</param>
    [AutomaticRetry(Attempts = 10)]
    public void ProcessDelayedFailingJob(int delaySeconds)
    {
        _logger.LogInformation("Processing delayed failing job - will fail after {DelaySeconds} seconds", delaySeconds);
        
        // Simulate some work with delay
        Thread.Sleep(delaySeconds * 1000);
        
        throw new InvalidOperationException($"This is a test exception after {delaySeconds} seconds delay to verify Slack retry filtering functionality.");
    }

    /// <summary>
    /// Processes a job that fails with different exception types to test various scenarios
    /// </summary>
    [AutomaticRetry(Attempts = 10)]
    public void ProcessFailingJobWithRandomException()
    {
        _logger.LogInformation("Processing failing job with random exception type");
        
        // Simulate some work
        Thread.Sleep(100);
        
        var random = new Random();
        var exceptionType = random.Next(3);
        
        Exception exception = exceptionType switch
        {
            0 => new InvalidOperationException("Random test exception 1"),
            1 => new ArgumentException("Random test exception 2"),
            _ => new TimeoutException("Random test exception 3")
        };
        
        throw exception;
    }
} 