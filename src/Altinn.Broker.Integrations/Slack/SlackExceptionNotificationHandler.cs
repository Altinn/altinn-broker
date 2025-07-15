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
    private string Channel => _slackSettings.NotificationChannel;

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
            null);

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
                null);
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
            "Job {JobId} failed on retry {RetryCount}", jobId, retryCount);

        try
        {
            await SendSlackNotificationWithMessage(exceptionMessage);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                null);
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
        var retryInfo = retryCount == 10 ? " (CRITICAL - Final retry)" : $" (Retry {retryCount})";
        
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
            Channel = Channel
        };
        await _slackClient.PostAsync(slackMessage);
    }
} 
