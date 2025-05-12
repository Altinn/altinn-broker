using System.Transactions;

using Hangfire;
using Hangfire.PostgreSql;

using Microsoft.Extensions.Logging;

using Npgsql;

using Polly;
using Polly.Retry;

namespace Altinn.Broker.Core.Helpers;
public static class TransactionWithRetriesPolicy
{
    public static async Task<T> Execute<T>(
        Func<CancellationToken, Task<T>> operation,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var result = await RetryPolicy(logger).ExecuteAndCaptureAsync<T>(async (cancellationToken) =>
        {
            using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var result = await operation(cancellationToken);
            transaction.Complete();
            return result;
        }, cancellationToken);
        if (result.Outcome == OutcomeType.Failure)
        {
            logger.LogError("Exception during retries: {message}\n{stackTrace}", result.FinalException.Message, result.FinalException.StackTrace);
            throw result.FinalException;
        }
        return result.Result;
    }

    public static AsyncRetryPolicy RetryPolicy(ILogger logger) => Policy
        .Handle<TransactionAbortedException>()
        .Or<PostgresException>()
        .Or<BackgroundJobClientException>()
        .Or<PostgreSqlDistributedLockException>()
        .WaitAndRetryAsync(
            8,
            retryAttempt => TimeSpan.FromMilliseconds(Math.Min(5 * Math.Pow(2, retryAttempt), 640)),
            (exception, timeSpan, retryCount, context) => 
            {
                logger.LogWarning($"Attempt {retryCount} failed with exception {exception.Message}. Retrying in {timeSpan.Milliseconds} seconds.");
            }
        );
}
