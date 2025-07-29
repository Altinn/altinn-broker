using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Retry;

namespace Altinn.Broker.Persistence.Helpers;

/// <summary>
/// Helper class to execute database commands with retry logic for transient failures
/// </summary>
public class ExecuteDBCommandWithRetries(ILogger<ExecuteDBCommandWithRetries> logger)
{
    private readonly ILogger<ExecuteDBCommandWithRetries> _logger = logger;

    /// <summary>
    /// Executes a database command with retry logic for transient failures
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the operation</returns>
    public async Task<T> ExecuteWithRetry<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var result = await CreateRetryPolicy().ExecuteAndCaptureAsync(operation, cancellationToken);
        
        if (result.Outcome == OutcomeType.Failure)
        {
            _logger.LogError("Exception during database command retries: {message}\n{stackTrace}", 
                result.FinalException.Message, 
                result.FinalException.StackTrace);
            throw result.FinalException;
        }
        
        return result.Result;
    }

    private AsyncRetryPolicy CreateRetryPolicy()
    {
        return Policy
            .Handle<NpgsqlException>(ex => ex.IsTransient)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromMilliseconds(100),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("Database command attempt {RetryCount} failed with exception: {ExceptionMessage}. Retrying in {RetryTimeSpan}ms...",
                        retryCount,
                        exception.Message,
                        timeSpan.TotalMilliseconds);
                });
    }
} 
