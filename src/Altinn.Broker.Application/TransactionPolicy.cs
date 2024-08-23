﻿using System.Transactions;

using Hangfire;

using Microsoft.Extensions.Logging;

using Npgsql;

using Polly;
using Polly.Retry;

namespace Altinn.Broker.Application;
public static class TransactionPolicy
{
    public static AsyncRetryPolicy RetryPolicy(ILogger logger) => Policy
        .Handle<TransactionAbortedException>()
        .Or<PostgresException>()
        .Or<BackgroundJobClientException>()
        .WaitAndRetryAsync(
            10,
            retryAttempt => TimeSpan.FromMilliseconds(0),
            (exception, timeSpan, retryCount, context) =>
            {
                logger.LogWarning($"Attempt {retryCount} failed with exception {exception.Message}. Retrying in {timeSpan.TotalSeconds} seconds.");
            }
        );
}