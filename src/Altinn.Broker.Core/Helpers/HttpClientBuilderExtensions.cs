using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace Altinn.Broker.Core.Helpers;

/// <summary>
/// Extension methods for configuring HttpClient with retry policies
/// </summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Adds standard retry policy to HttpClient
    /// </summary>
    public static IHttpClientBuilder AddStandardRetryPolicy(this IHttpClientBuilder builder)
    {
        return builder.AddPolicyHandler((services, request) =>
        {
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("HttpRetryPolicies");
            return GetStandardRetryPolicy(logger);
        });
    }

    /// <summary>
    /// A standard retry policy for HTTP operations; 3 retry attempts with exponential backoff (50ms, 100ms, 200ms)
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetStandardRetryPolicy(ILogger logger)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && IsTransientFailure(r.StatusCode))
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 50),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var exception = outcome.Exception;
                    var result = outcome.Result;
                    
                    if (exception != null)
                    {
                        logger.LogWarning("HTTP request attempt {RetryCount} failed with exception: {Exception}. Retrying in {Delay}ms", 
                            retryCount, exception.Message, timespan.TotalMilliseconds);
                    }
                    else if (result != null)
                    {
                        logger.LogWarning("HTTP request attempt {RetryCount} failed with status {StatusCode}. Retrying in {Delay}ms", 
                            retryCount, result.StatusCode, timespan.TotalMilliseconds);
                    }
                });
    }

    /// <summary>
    /// Determines if an HTTP status code indicates a transient failure
    /// </summary>
    private static bool IsTransientFailure(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.RequestTimeout => true,
            HttpStatusCode.TooManyRequests => true,
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            _ => false
        };
    }
} 