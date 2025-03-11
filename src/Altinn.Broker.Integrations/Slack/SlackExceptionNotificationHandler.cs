using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Slack.Webhooks;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Altinn.Broker.Core.Options;

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
            "Unhandled exception occurred. Type: {ExceptionType}, Message: {Message}, Path: {Path}",
            exception.GetType().Name,
            exception.Message,
            httpContext.Request.Path);

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
                "Failed to send Slack notification");
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
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification");
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