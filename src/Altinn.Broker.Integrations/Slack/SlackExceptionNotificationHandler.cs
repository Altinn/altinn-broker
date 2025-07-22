using System.Diagnostics;
using System.Net;

using Altinn.Broker.Core.Options;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Slack.Webhooks;

namespace Altinn.Broker.Integrations.Slack;

public class SlackExceptionNotificationHandler : IExceptionHandler
{
    private readonly ILogger<SlackExceptionNotificationHandler> _logger;
    private readonly ISlackClient _slackClient;
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly SlackSettings _slackSettings;

    public SlackExceptionNotificationHandler(
        ILogger<SlackExceptionNotificationHandler> logger,
        ISlackClient slackClient,
        IProblemDetailsService problemDetailsService,
        IHostEnvironment hostEnvironment,
        SlackSettings slackSettings)
    {
        _logger = logger;
        _slackClient = slackClient;
        _problemDetailsService = problemDetailsService;
        _hostEnvironment = hostEnvironment;
        _slackSettings = slackSettings;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var exceptionMessage = FormatExceptionMessage(exception, httpContext);

        _logger.LogError(
            exception,
            "Unhandled exception occurred. Type: {ExceptionType}, Message: {Message}, Path: {Path}, Environment: {Environment}, System: {System}, StackTrace: {StackTrace}, ExceptionSource: {ExceptionSource}, ExceptionMessage: {ExceptionMessage}, SentToSlack: {SentToSlack}, SlackMessage: {SlackMessage}",
            exception.GetType().Name,
            exception.Message,
            httpContext.Request.Path,
            _hostEnvironment.EnvironmentName,
            "Broker",
            exception.StackTrace ?? "No stack trace available",
            "HTTP",
            exception.Message,
            true, // Slack notification attempted
            exceptionMessage
        );

        try
        {
            await SendSlackNotificationWithMessage(exceptionMessage);

            var statusCode = HttpStatusCode.InternalServerError;
            var problemDetails = new ProblemDetails
            {
                Status = (int)statusCode,
                Title = "Internal Server Error",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Detail = _hostEnvironment.IsDevelopment() ? exception.Message : "",
                Instance = httpContext.Request.Path
            };
            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            problemDetails.Extensions["traceId"] = traceId;

            await _problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails
            });

            return true;
        }
        catch (Exception slackEx)
        {
            _logger.LogError(
                slackEx,
                "Failed to send Slack notification. OriginalExceptionType: {OriginalExceptionType}, SlackExceptionType: {SlackExceptionType}, Environment: {Environment}, System: {System}, StackTrace: {StackTrace}, ExceptionSource: {ExceptionSource}, ExceptionMessage: {ExceptionMessage}, SentToSlack: {SentToSlack}, SlackMessage: {SlackMessage}",
                exception.GetType().Name,
                slackEx.GetType().Name,
                _hostEnvironment.EnvironmentName,
                "Broker",
                slackEx.StackTrace ?? "No stack trace available",
                "SlackNotification",
                slackEx.Message,
                false, // Slack notification failed
                exceptionMessage
            );
            return true;
        }
    }

    public async Task<bool> TryHandleBackgroundJobAsync(string jobId, string jobName, Exception exception, int retryCount = 0)
    {
        // Only send Slack notifications on retry 3, 6 and 10 (final retry)
        if (retryCount != 3 && retryCount != 6 && retryCount != 10)
        {
            _logger.LogDebug("Skipping Slack notification for job {JobId} on retry {RetryCount}", jobId, retryCount);
            return true;
        }

        var exceptionMessage = FormatBackgroundJobExceptionMessage(jobId, jobName, exception, retryCount);

        _logger.LogError(
            exception,
            "Background job failed. JobId: {JobId}, JobName: {JobName}, RetryCount: {RetryCount}, ExceptionType: {ExceptionType}, Environment: {Environment}, System: {System}, StackTrace: {StackTrace}, ExceptionSource: {ExceptionSource}, ExceptionMessage: {ExceptionMessage}, SentToSlack: {SentToSlack}, SlackMessage: {SlackMessage}",
            jobId,
            jobName,
            retryCount,
            exception.GetType().Name,
            _hostEnvironment.EnvironmentName,
            "Broker",
            exception.StackTrace ?? "No stack trace available",
            "BackgroundJob",
            exception.Message,
            true, // Slack notification attempted
            exceptionMessage
        );

        try
        {
            await SendSlackNotificationWithMessage(exceptionMessage);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send Slack notification for background job. OriginalExceptionType: {OriginalExceptionType}, SlackExceptionType: {SlackExceptionType}, JobId: {JobId}, JobName: {JobName}, RetryCount: {RetryCount}, Environment: {Environment}, System: {System}, StackTrace: {StackTrace}, ExceptionSource: {ExceptionSource}, ExceptionMessage: {ExceptionMessage}, SentToSlack: {SentToSlack}, SlackMessage: {SlackMessage}",
                exception.GetType().Name,
                ex.GetType().Name,
                jobId,
                jobName,
                retryCount,
                _hostEnvironment.EnvironmentName,
                "Broker",
                ex.StackTrace ?? "No stack trace available",
                "SlackNotification",
                ex.Message,
                false, // Slack notification failed
                exceptionMessage
            );
            return false;
        }
    }

    private string FormatExceptionMessage(Exception exception, HttpContext context)
    {
        return $":warning: *Unhandled Exception*\n" +
               $"*Environment:* {_hostEnvironment.EnvironmentName}\n" +
               $"*System:* Broker\n" +
               $"*Type:* {exception.GetType().Name}\n" +
               $"*Message:* {exception.Message}\n" +
               $"*Path:* {context.Request.Path}\n" +
               $"*Time:* {DateTime.UtcNow:u}\n" +
               $"*Stacktrace:* \n{exception.StackTrace}";
    }

    private string FormatBackgroundJobExceptionMessage(string jobId, string jobName, Exception exception, int retryCount)
    {
        var severity = retryCount == 10 ? ":rotating_light:" : ":warning:";
        
        return $"{severity} *Unhandled Exception in Background Job*\n" +
               $"*Environment:* {_hostEnvironment.EnvironmentName}\n" +
               $"*System:* Broker\n" +
               $"*Job ID:* {jobId}\n" +
               $"*Job Name:* {jobName}\n" +
               $"*Type:* {exception.GetType().Name}\n" +
               $"*Message:* {exception.Message}\n" +
               $"*Retry Count:* {retryCount}\n" +
               $"*Time:* {DateTime.UtcNow:u}\n" +
               $"*Stacktrace:* \n{exception.StackTrace}";
    }

    private async Task SendSlackNotificationWithMessage(string message)
    {
        var slackMessage = new SlackMessage
        {
            Text = message,
            Channel = _slackSettings.NotificationChannel
        };
        await _slackClient.PostAsync(slackMessage);
    }
} 
