using Microsoft.AspNetCore.Diagnostics;
using Slack.Webhooks;

namespace Altinn.Broker.Helpers;
public class SlackExceptionNotification : IExceptionHandler
{
    private readonly ILogger<SlackExceptionNotification> _logger;
    private readonly ISlackClient _slackClient;
    private const string TestChannel = "#test-varslinger";
    private readonly IHostEnvironment _hostEnvironment;

    public SlackExceptionNotification(ILogger<SlackExceptionNotification> logger, ISlackClient slackClient, IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _slackClient = slackClient;
        _hostEnvironment = hostEnvironment;

    }
    public ValueTask<bool> TryHandleAsync(
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
            SendSlackNotificationWithMessage(exceptionMessage);
        }
        catch (Exception slackEx)
        {
            _logger.LogError(
                slackEx,
                "Failed to send Slack notification");
        }

        return ValueTask.FromResult(false);
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
    private void SendSlackNotificationWithMessage(string message)
    {
        var slackMessage = new SlackMessage
        {
            Text = message,
            Channel = TestChannel,
        };
        _slackClient.Post(slackMessage);
    }
}