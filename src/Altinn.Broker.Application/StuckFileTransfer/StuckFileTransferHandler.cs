using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application;

public class StuckFileTransferHandler
{
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly ILogger<StuckFileTransferHandler> _logger;
    private readonly SlackStuckFileTransferNotifier _slackNotifier;
    private readonly int _stuckThresholdMinutes = 15;

    public StuckFileTransferHandler(
        IFileTransferStatusRepository fileTransferStatusRepository,
        SlackStuckFileTransferNotifier slackNotifier,
        ILogger<StuckFileTransferHandler> logger)
    {
        _fileTransferStatusRepository = fileTransferStatusRepository;
        _slackNotifier = slackNotifier;
        _logger = logger;
    }

    public async Task CheckForStuckFileTransfers(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking for file transfers stuck in upload processing or upload started");
        List<FileTransferStatusEntity> stuckFileTransfers = await _fileTransferStatusRepository.GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(
            new List<FileTransferStatus> { FileTransferStatus.UploadProcessing, FileTransferStatus.UploadStarted }, 
            DateTime.UtcNow.AddMinutes(-_stuckThresholdMinutes), 
            cancellationToken);

        foreach (FileTransferStatusEntity status in stuckFileTransfers)
        {
            _logger.LogWarning("File transfer {fileTransferId} has been stuck in {status} for more than {thresholdMinutes} minutes", status.FileTransferId, status.Status, _stuckThresholdMinutes);
            var succesfullNotification = await _slackNotifier.NotifyFileStuckWithStatus(status);
            if (!succesfullNotification)
            {
                _logger.LogError("Failed to send Slack notification for file transfer {fileTransferId}", status.FileTransferId);
            }
        }
    }
}