using System.Security.Cryptography;
using System.Text;

using Altinn.Broker.Application.Middlewares;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services.Enums;

using Hangfire;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.UploadFile;

public class FileTransferPublishService(
    IFileTransferStatusRepository fileTransferStatusRepository,
    IIdempotencyEventRepository idempotencyEventRepository,
    IBackgroundJobClient backgroundJobClient,
    EventBusMiddleware eventBus,
    ILogger<FileTransferPublishService> logger)
{
    public const string PublishedIdempotencySuffix = "_published";

    public static string GetPublishedClaimKey(Guid fileTransferId) =>
        $"{fileTransferId}{PublishedIdempotencySuffix}";

    /// <summary>
    /// Atomically claims publish for a file transfer, inserts Published status, and enqueues Published events.
    /// Returns false if another caller already claimed publish.
    /// </summary>
    public async Task<bool> TryPublishAsync(
        FileTransferEntity fileTransfer,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var claimed = await idempotencyEventRepository.TryAddIdempotencyEventAsync(
            GetPublishedClaimKey(fileTransfer.FileTransferId),
            cancellationToken);
        if (!claimed)
        {
            logger.LogInformation(
                "Skipping publish for {fileTransferId}; publish already claimed",
                fileTransfer.FileTransferId);
            return false;
        }

        await fileTransferStatusRepository.InsertFileTransferStatus(
            fileTransfer.FileTransferId,
            FileTransferStatus.Published,
            timestamp: timestamp,
            cancellationToken: cancellationToken);

        EnqueuePublishedEvents(fileTransfer);
        return true;
    }

    private void EnqueuePublishedEvents(FileTransferEntity fileTransfer)
    {
        backgroundJobClient.Enqueue(() => eventBus.Publish(
            AltinnEventType.Published,
            fileTransfer.ResourceId,
            fileTransfer.FileTransferId.ToString(),
            fileTransfer.Sender.ActorExternalId,
            CreateStablePublishedEventId(
                fileTransfer.FileTransferId,
                AltinnEventSubjectRole.Sender,
                fileTransfer.Sender.ActorExternalId),
            AltinnEventSubjectRole.Sender));

        foreach (var recipient in fileTransfer.RecipientCurrentStatuses)
        {
            backgroundJobClient.Enqueue(() => eventBus.Publish(
                AltinnEventType.Published,
                fileTransfer.ResourceId,
                fileTransfer.FileTransferId.ToString(),
                recipient.Actor.ActorExternalId,
                CreateStablePublishedEventId(
                    fileTransfer.FileTransferId,
                    AltinnEventSubjectRole.Recipient,
                    recipient.Actor.ActorExternalId),
                AltinnEventSubjectRole.Recipient));
        }
    }

    public static Guid CreateStablePublishedEventId(
        Guid fileTransferId,
        AltinnEventSubjectRole subjectRole,
        string actorExternalId)
    {
        var name = $"published:{fileTransferId}:{subjectRole}:{actorExternalId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return new Guid(hash.AsSpan(0, 16));
    }
}
