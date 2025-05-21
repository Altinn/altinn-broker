using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Slack.Webhooks;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Altinn.Broker.Core.Options;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Altinn.Broker.Integrations.Slack;

public class SlackExceptionNotificationHandler : IExceptionHandler
{
    private readonly ILogger<SlackExceptionNotificationHandler> _logger;
    private readonly ISlackClient _slackClient;
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly SlackSettings _slackSettings;
    private readonly TelemetryClient _telemetryClient;
    private string Channel => _slackSettings.NotificationChannel;

    public SlackExceptionNotificationHandler(
        ILogger<SlackExceptionNotificationHandler> logger,
        ISlackClient slackClient,
        IProblemDetailsService problemDetailsService,
        IHostEnvironment hostEnvironment,
        SlackSettings slackSettings,
        TelemetryClient telemetryClient)
    {
        _logger = logger;
        _slackClient = slackClient;
        _problemDetailsService = problemDetailsService;
        _hostEnvironment = hostEnvironment;
        _slackSettings = slackSettings;
        _telemetryClient = telemetryClient;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var exceptionMessage = FormatExceptionMessage(exception, httpContext);

        _logger.LogError(
            exception,
            "Unhandled exception occurred. Type: {ExceptionType}, Message: {Message}, Path: {Path}",
            exception.GetType().Name,
            exception.Message,
            httpContext.Request.Path.ToString().Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", ""));

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

            // Track successful Slack notification
            var properties = new Dictionary<string, string>
            {
                { "ExceptionType", exception.GetType().Name },
                { "Path", httpContext.Request.Path },
                { "Environment", _hostEnvironment.EnvironmentName },
                { "System", "Broker" },
                { "StackTrace", exception.StackTrace ?? "No stack trace available" },
                { "InnerExceptionStackTrace", exception.InnerException?.StackTrace ?? "No inner exception stack trace" },
                { "ExceptionSource", "HTTP" },
                { "ExceptionIdentifier", $"{exception.GetType().Name}:{httpContext.Request.Path}" },
                { "ExceptionMessage", exception.Message },
                { "InnerExceptionType", exception.InnerException?.GetType().Name ?? "None" },
                { "InnerExceptionMessage", exception.InnerException?.Message ?? "None" },
                { "SentToSlack", "true" },
                { "SlackMessage", exceptionMessage }
            };

            _telemetryClient.TrackException(exception, properties);

            return true;
        }
        catch (Exception slackEx)
        {
            _logger.LogError(
                slackEx,
                "Failed to send Slack notification");

            // Track Slack notification failure
            var properties = new Dictionary<string, string>
            {
                { "OriginalExceptionType", exception.GetType().Name },
                { "SlackExceptionType", slackEx.GetType().Name },
                { "Environment", _hostEnvironment.EnvironmentName },
                { "System", "Broker" },
                { "StackTrace", slackEx.StackTrace ?? "No stack trace available" },
                { "InnerExceptionStackTrace", slackEx.InnerException?.StackTrace ?? "No inner exception stack trace" },
                { "ExceptionSource", "SlackNotification" },
                { "ExceptionIdentifier", $"SlackNotification:{exception.GetType().Name}:{httpContext.Request.Path}" },
                { "ExceptionMessage", slackEx.Message },
                { "InnerExceptionType", slackEx.InnerException?.GetType().Name ?? "None" },
                { "InnerExceptionMessage", slackEx.InnerException?.Message ?? "None" }
            };

            _telemetryClient.TrackException(slackEx, properties);

            return true;
        }
    }

    public async Task<bool> TryHandleBackgroundJobAsync(string jobId, string jobName, Exception exception)
    {
        var exceptionMessage = FormatBackgroundJobExceptionMessage(jobId, jobName, exception);

        _logger.LogError(
            exception,
            "Unhandled exception occurred in background job. Job ID: {JobId}, Job Name: {JobName}, Type: {ExceptionType}, Message: {Message}",
            jobId,
            jobName,
            exception.GetType().Name,
            exception.Message);

        try
        {
            await SendSlackNotificationWithMessage(exceptionMessage);

            // Track successful Slack notification for background job
            var properties = new Dictionary<string, string>
            {
                { "ExceptionType", exception.GetType().Name },
                { "JobId", jobId },
                { "JobName", jobName },
                { "Environment", _hostEnvironment.EnvironmentName },
                { "System", "Broker" },
                { "StackTrace", exception.StackTrace ?? "No stack trace available" },
                { "InnerExceptionStackTrace", exception.InnerException?.StackTrace ?? "No inner exception stack trace" },
                { "ExceptionSource", "BackgroundJob" },
                { "ExceptionIdentifier", $"BackgroundJob:{exception.GetType().Name}:{jobName}" },
                { "ExceptionMessage", exception.Message },
                { "InnerExceptionType", exception.InnerException?.GetType().Name ?? "None" },
                { "InnerExceptionMessage", exception.InnerException?.Message ?? "None" },
                { "SentToSlack", "true" },
                { "SlackMessage", exceptionMessage }
            };

            _telemetryClient.TrackException(exception, properties);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification");

            // Track Slack notification failure for background job
            var properties = new Dictionary<string, string>
            {
                { "OriginalExceptionType", exception.GetType().Name },
                { "SlackExceptionType", ex.GetType().Name },
                { "JobId", jobId },
                { "JobName", jobName },
                { "Environment", _hostEnvironment.EnvironmentName },
                { "System", "Broker" },
                { "StackTrace", ex.StackTrace ?? "No stack trace available" },
                { "InnerExceptionStackTrace", ex.InnerException?.StackTrace ?? "No inner exception stack trace" },
                { "ExceptionSource", "BackgroundJobSlackNotification" },
                { "ExceptionIdentifier", $"BackgroundJobSlackNotification:{exception.GetType().Name}:{jobName}" },
                { "ExceptionMessage", ex.Message },
                { "InnerExceptionType", ex.InnerException?.GetType().Name ?? "None" },
                { "InnerExceptionMessage", ex.InnerException?.Message ?? "None" }
            };

            _telemetryClient.TrackException(ex, properties);

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

    private string FormatBackgroundJobExceptionMessage(string jobId, string jobName, Exception exception)
    {
        return $":warning: *Unhandled Exception in Background Job*\n" +
               $"*Environment:* {_hostEnvironment.EnvironmentName}\n" +
               $"*System:* Broker\n" +
               $"*Job ID:* {jobId}\n" +
               $"*Job Name:* {jobName}\n" +
               $"*Type:* {exception.GetType().Name}\n" +
               $"*Message:* {exception.Message}\n" +
               $"*Time:* {DateTime.UtcNow:u}\n" +
               $"*Stacktrace:* \n{exception.StackTrace}";
    }

    private async Task SendSlackNotificationWithMessage(string message)
    {
        var slackMessage = new SlackMessage
        {
            Text = message,
            Channel = Channel
        };
        await _slackClient.PostAsync(slackMessage);
    }
} 