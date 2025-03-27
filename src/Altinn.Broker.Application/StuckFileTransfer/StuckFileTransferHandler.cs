using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.FileTransferMonitor;

public class StuckFileTransferHandler(
    IFileTransferStatusRepository fileTransferStatusRepository,
    SlackStuckFileTransferNotifier slackNotifier,
    ILogger<StuckFileTransferHandler> logger)
{
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository = fileTransferStatusRepository;
    private readonly ILogger<StuckFileTransferHandler> _logger = logger;
    private readonly HashSet<Guid> _ongoingStuckFileTransferIds = new HashSet<Guid>();
    private readonly SlackStuckFileTransferNotifier _slackNotifier = slackNotifier;
    private readonly int _stuckThresholdMinutes = 15;

    public async Task CheckForStuckFileTransfers(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking for file transfers stuck in upload processing");
        List<FileTransferStatusEntity> fileTransfersStuckInUploadProcessing = await _fileTransferStatusRepository.GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(
                FileTransferStatus.UploadProcessing, 
                DateTime.UtcNow.AddMinutes(-_stuckThresholdMinutes), 
                cancellationToken);
        
        foreach (FileTransferStatusEntity status in fileTransfersStuckInUploadProcessing)
        {
            if (!_ongoingStuckFileTransferIds.Contains(status.FileTransferId))
            {
            _logger.LogWarning("File transfer {fileTransferId} has been stuck in upload processing for more than {thresholdMinutes} minutes", status.FileTransferId, _stuckThresholdMinutes);
            _ongoingStuckFileTransferIds.Add(status.FileTransferId);
            _slackNotifier.NotifyFileStuckWithStatus(status);
            }
        }

        foreach (Guid fileTransferId in _ongoingStuckFileTransferIds)
        {
            if (!fileTransfersStuckInUploadProcessing.Any(s => s.FileTransferId == fileTransferId))
            {
                _logger.LogInformation("File transfer {fileTransferId} is no longer stuck in upload processing", fileTransferId);
                _ongoingStuckFileTransferIds.Remove(fileTransferId);
            }
        }
    }
}