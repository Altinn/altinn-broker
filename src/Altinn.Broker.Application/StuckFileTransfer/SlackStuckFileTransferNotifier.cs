using Altinn.Broker.Core.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Slack.Webhooks;

namespace Altinn.Broker.Application.FileTransferMonitor;
public class SlackStuckFileTransferNotifier
{
    private readonly ILogger<SlackStuckFileTransferNotifier> _logger;
    private readonly ISlackClient _slackClient;
    private const string TestChannel = "#test-varslinger";
    private readonly IHostEnvironment _hostEnvironment;

    public SlackStuckFileTransferNotifier(ILogger<SlackStuckFileTransferNotifier> logger, ISlackClient slackClient, IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _slackClient = slackClient;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<bool> NotifyFileStuckWithStatus(
        FileTransferStatusEntity fileTransferStatus)
    {
        var errorMessage = FormatNotificationMessage(fileTransferStatus);
        try
        {
            return await SendSlackNotificationWithMessage(errorMessage);
        }
        catch (Exception slackEx)
        {
            _logger.LogError(
                slackEx,
                "Failed to send Slack notification");
            return false;
        }
    }

    private string FormatNotificationMessage(FileTransferStatusEntity fileTransferStatus)
    {
        return $":warning: *FileTransfer stuck with status*\n" +
               $"*Environment:* {_hostEnvironment.EnvironmentName}\n" +
               $"*System:* Broker\n" +
               $"*File transfer id:* {fileTransferStatus.FileTransferId}\n" +
               $"*Status:* {fileTransferStatus.Status}\n" +
               $"*Status start date:* {fileTransferStatus.Date}\n" +
               $"*Time:* {DateTime.UtcNow:u}\n";
    }
    private async Task<bool> SendSlackNotificationWithMessage(string message)
    {
        var slackMessage = new SlackMessage
        {
            Text = message,
            Channel = TestChannel,
        };
        return await _slackClient.PostAsync(slackMessage);
    }
}