using Microsoft.AspNetCore.Diagnostics;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Integrations.Slack;

namespace Altinn.Broker.Helpers;

public class SlackExceptionNotification : IExceptionHandler
{
    private readonly ILogger<SlackExceptionNotification> _logger;
    private readonly SlackNotificationService _slackService;
    private readonly SlackSettings _slackSettings;
    private readonly IHostEnvironment _hostEnvironment;

    public SlackExceptionNotification(
        ILogger<SlackExceptionNotification> logger,
        SlackNotificationService slackService,
        SlackSettings slackSettings,
        IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _slackService = slackService;
        _slackSettings = slackSettings;
        _hostEnvironment = hostEnvironment;
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
            return true;
        }
        catch (Exception slackEx)
        {
            _logger.LogError(
                slackEx,
                "Failed to send Slack notification");
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

    private async Task SendSlackNotificationWithMessage(string message)
    {
        await _slackService.SendSlackMessageAsync(message);
    }
}