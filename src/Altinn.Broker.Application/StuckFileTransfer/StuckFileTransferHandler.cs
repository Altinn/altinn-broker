using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application;

public class StuckFileTransferHandler
{
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly ILogger<StuckFileTransferHandler> _logger;
    private readonly HashSet<Guid> _ongoingStuckFileTransferIds = new HashSet<Guid>();
    private readonly SlackStuckFileTransferNotifier _slackNotifier;
    private readonly int _stuckThresholdMinutes = 5;

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
        _logger.LogInformation("Checking for file transfers stuck in upload processing");
        List<FileTransferStatusEntity> fileTransfersStuckInUploadProcessing = await _fileTransferStatusRepository.GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(
            FileTransferStatus.UploadProcessing, 
            DateTime.UtcNow.AddMinutes(-_stuckThresholdMinutes), 
            cancellationToken);

        if (fileTransfersStuckInUploadProcessing.Count == 0)
        {
            _logger.LogInformation("No file transfers are stuck in upload processing, creating a fictional filetransferEntity to test slack notification");
            var fictionalFileTransferStatusEntity = new FileTransferStatusEntity
            {
                FileTransferId = Guid.NewGuid(),
                Status = FileTransferStatus.UploadProcessing,
                Date = DateTime.UtcNow.AddMinutes(-_stuckThresholdMinutes)
            };
            fileTransfersStuckInUploadProcessing.Add(fictionalFileTransferStatusEntity);
        }

        foreach (FileTransferStatusEntity status in fileTransfersStuckInUploadProcessing)
        {
            if (!_ongoingStuckFileTransferIds.Contains(status.FileTransferId))
            {
                _logger.LogWarning("File transfer {fileTransferId} has been stuck in upload processing for more than {thresholdMinutes} minutes", status.FileTransferId, _stuckThresholdMinutes);
                _ongoingStuckFileTransferIds.Add(status.FileTransferId);
                var succesfullNotification = await _slackNotifier.NotifyFileStuckWithStatus(status);
                if (!succesfullNotification)
                {
                    _logger.LogError("Failed to send Slack notification for file transfer {fileTransferId}", status.FileTransferId);
                }
            }
        }

        HashSet<Guid> transferIdsToRemove = new HashSet<Guid>();
        foreach (Guid fileTransferId in _ongoingStuckFileTransferIds)
        {
            if (!fileTransfersStuckInUploadProcessing.Any(s => s.FileTransferId == fileTransferId))
            {
                _logger.LogInformation("File transfer {fileTransferId} is no longer stuck in upload processing", fileTransferId);
                transferIdsToRemove.Add(fileTransferId);
             }
        }
        foreach (Guid idToRemove in transferIdsToRemove)
        {
            _ongoingStuckFileTransferIds.Remove(idToRemove);
        }
    }
}