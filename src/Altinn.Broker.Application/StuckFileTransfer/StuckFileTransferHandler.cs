using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application;

public class StuckFileTransferHandler(
    IFileTransferStatusRepository fileTransferStatusRepository,
    IFileTransferRepository fileTransferRepository,
    IBackgroundJobClient backgroundJobClient,
    SlackStuckFileTransferNotifier slackNotifier,
    ILogger<StuckFileTransferHandler> logger)
{
    private readonly int _stuckInUploadProcessingThresholdMinutes = 15;
    private readonly int _stuckInUploadStartingThresholdMinutes = 60 * 24;

    public async Task CheckForStuckFileTransfers(CancellationToken cancellationToken)
    {
        logger.LogInformation("Checking for file transfers stuck in upload started");

        var stuckInUploadStarted = await fileTransferStatusRepository.GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(
            new List<FileTransferStatus> { FileTransferStatus.UploadStarted },
            DateTime.UtcNow.AddMinutes(-_stuckInUploadStartingThresholdMinutes),
            cancellationToken);

        foreach (FileTransferStatusEntity fileTransferStatus in stuckInUploadStarted)
        {
            var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferStatus.FileTransferId, cancellationToken);
            if (fileTransfer is null)
            {
                throw new Exception("File transfer with id {fileTransferId} was not found in the database, even though it has a status entry.");
            }
            await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
            {
                logger.LogError("File transfer {fileTransferId} has been stuck in UploadStarted for more than {thresholdMinutes} minutes. Marking as failed.", fileTransferStatus.FileTransferId, _stuckInUploadStartingThresholdMinutes);
                await fileTransferStatusRepository.InsertFileTransferStatus(fileTransferStatus.FileTransferId, FileTransferStatus.Failed, "File transfer was stuck in UploadStarted as it failed upload mid-request.", cancellationToken);
                //backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.UploadFailed, fileTransfer.ResourceId, fileTransfer.FileTransferId.ToString(), fileTransfer.Sender.ActorExternalId, Guid.NewGuid(), AltinnEventSubjectRole.Sender, CancellationToken.None));
                return Task.CompletedTask;
            }, logger, cancellationToken);
        }

        logger.LogInformation("Checking for file transfers stuck in upload processing");
        var stuckInUploadProcessing = await fileTransferStatusRepository.GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(
            new List<FileTransferStatus> { FileTransferStatus.UploadProcessing },
            DateTime.UtcNow.AddMinutes(-_stuckInUploadProcessingThresholdMinutes),
            cancellationToken);
        foreach (FileTransferStatusEntity status in stuckInUploadProcessing)
        {
            logger.LogWarning("File transfer {fileTransferId} has been stuck in UploadProcessing for more than {thresholdMinutes} minutes", status.FileTransferId, _stuckInUploadProcessingThresholdMinutes);
            var succesfullNotification = await slackNotifier.NotifyFileStuckWithStatus(status);
            if (!succesfullNotification)
            {
                logger.LogError("Failed to send Slack notification for file transfer {fileTransferId}", status.FileTransferId);
            }
        }
    }
}
