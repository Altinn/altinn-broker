using Altinn.Broker.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Slack.Webhooks;

namespace Altinn.Broker.Application.SendSlackNotification;

/// <summary>
/// Handler for sending operational Slack notifications.
/// </summary>
public class SendSlackNotificationHandler(
    ISlackClient slackClient,
    SlackSettings slackSettings,
    IHostEnvironment hostEnvironment,
    ILogger<SendSlackNotificationHandler> logger)
{
    public async Task Process(string title, string message, string emoji = ":warning:")
    {
        var text =
            $"{emoji} *{title}*\n" +
            $"*Environment:* {hostEnvironment.EnvironmentName}\n" +
            $"*System:* Broker\n" +
            $"*Message:* {message}\n" +
            $"*Time:* {DateTime.UtcNow:u}\n";

        var slackMessage = new SlackMessage
        {
            Text = text,
            Channel = slackSettings.NotificationChannel
        };

        try
        {
            await slackClient.PostAsync(slackMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Slack notification. Title={Title}", title);
        }
    }
}
