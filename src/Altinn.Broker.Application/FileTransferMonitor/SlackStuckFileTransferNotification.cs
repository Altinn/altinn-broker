using Altinn.Broker.Core.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Slack.Webhooks;

namespace Altinn.Broker.Application.FileTransferMonitor;
public class SlackStuckFileTransferNotification
{
    private readonly ILogger<SlackStuckFileTransferNotification> _logger;
    private readonly ISlackClient _slackClient;
    private const string TestChannel = "#test-varslinger";
    private readonly IHostEnvironment _hostEnvironment;

    public SlackStuckFileTransferNotification(ILogger<SlackStuckFileTransferNotification> logger, ISlackClient slackClient, IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _slackClient = slackClient;
        _hostEnvironment = hostEnvironment;
    }
    
    public bool NotifyFileStuckWithStatus(
        FileTransferStatusEntity fileTransferStatus,
        CancellationToken cancellationToken)
    {
        var errorMessage = FormatNotificationMessage(fileTransferStatus);

        _logger.LogWarning("File transfer {fileTransferId} has been stuck in status {status} for more than 15 minutes", fileTransferStatus.FileTransferId, fileTransferStatus.Status);
        
        try
        {
            SendSlackNotificationWithMessage(errorMessage);
        }
        catch (Exception slackEx)
        {
            _logger.LogError(
                slackEx,
                "Failed to send Slack notification");
            return false;
        }

        return true;
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