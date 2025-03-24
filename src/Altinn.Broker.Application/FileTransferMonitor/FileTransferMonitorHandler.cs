using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.FileTransferMonitorHandler;

public class FileTransferMonitorHandler(
    IFileTransferStatusRepository fileTransferStatusRepository,
    ILogger<FileTransferMonitorHandler> logger)
{
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository = fileTransferStatusRepository;
    private readonly ILogger<FileTransferMonitorHandler> _logger = logger;

    public async Task CheckForStuckFileTransfers(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking for file transfers stuck in upload processing");
        List<FileTransferStatusEntity> fileTransferStatuses = await _fileTransferStatusRepository.GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(FileTransferStatus.UploadProcessing, DateTime.UtcNow.AddMinutes(-10), cancellationToken);
        foreach (FileTransferStatusEntity status in fileTransferStatuses)
        {
            _logger.LogWarning("File transfer {fileTransferId} has been stuck in upload processing for more than 15 minutes", status.FileTransferId);
        }
    }
}