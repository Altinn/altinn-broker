using System.ComponentModel;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class FileTransferStatusOverviewExtMapper
{
    internal static FileTransferOverviewExt MapToExternalModel(FileTransferEntity fileTransfer, List<FileTransferStatusEntity> fileTransferEvents)
    {
        var overview = new FileTransferOverviewExt();
        MapBaseProperties(fileTransfer, fileTransferEvents, overview);
        return overview;
    }

    internal static T MapBaseProperties<T>(FileTransferEntity fileTransfer, List<FileTransferStatusEntity> fileTransferEvents, T target) where T : FileTransferOverviewExt
    {
        var publishedEvent = fileTransferEvents.FirstOrDefault(e => e.Status == FileTransferStatus.Published);
        
        target.ResourceId = fileTransfer.ResourceId;
        target.FileTransferSize = fileTransfer.FileTransferSize;
        target.FileTransferId = fileTransfer.FileTransferId;
        target.FileName = fileTransfer.FileName;
        target.FileTransferStatus = MapToExternalEnum(fileTransfer.FileTransferStatusEntity.Status);
        target.Sender = fileTransfer.Sender.ActorExternalId;
        target.FileTransferStatusChanged = fileTransfer.FileTransferStatusChanged;
        target.FileTransferStatusText = MapToFileTransferStatusText(fileTransfer.FileTransferStatusEntity);
        target.PropertyList = fileTransfer.PropertyList;
        target.Published = publishedEvent?.Date;
        target.Recipients = MapToRecipients(fileTransfer.RecipientCurrentStatuses);
        target.SendersFileTransferReference = fileTransfer.SendersFileTransferReference;
        target.Created = fileTransfer.Created;
        target.ExpirationTime = fileTransfer.ExpirationTime;
        target.Checksum = fileTransfer.Checksum;
        target.UseVirusScan = fileTransfer.UseVirusScan;
        
        return target;
    }

    internal static FileTransferStatusExt MapToExternalEnum(FileTransferStatus domainEnum)
    {
        return domainEnum switch
        {
            FileTransferStatus.Initialized => FileTransferStatusExt.Initialized,
            FileTransferStatus.UploadStarted => FileTransferStatusExt.UploadStarted,
            FileTransferStatus.UploadProcessing => FileTransferStatusExt.UploadProcessing,
            FileTransferStatus.Published => FileTransferStatusExt.Published,
            FileTransferStatus.Cancelled => FileTransferStatusExt.Cancelled,
            FileTransferStatus.AllConfirmedDownloaded => FileTransferStatusExt.AllConfirmedDownloaded,
            FileTransferStatus.Purged => FileTransferStatusExt.Purged,
            FileTransferStatus.Failed => FileTransferStatusExt.Failed,
            _ => throw new InvalidEnumArgumentException()
        };
    }

    internal static string MapToFileTransferStatusText(FileTransferStatusEntity fileTransferStatusEntity)
    {
        if (!string.IsNullOrWhiteSpace(fileTransferStatusEntity.DetailedStatus))
        {
            return fileTransferStatusEntity.DetailedStatus;
        }
        return fileTransferStatusEntity.Status switch
        {
            FileTransferStatus.Initialized => "Ready for upload",
            FileTransferStatus.UploadStarted => "Upload started",
            FileTransferStatus.UploadProcessing => "Processing upload",
            FileTransferStatus.Published => "Ready for download",
            FileTransferStatus.Cancelled => "FileTransfer cancelled",
            FileTransferStatus.AllConfirmedDownloaded => "All downloaded",
            FileTransferStatus.Purged => "FileTransfer has been purged",
            FileTransferStatus.Failed => "Upload failed",
            _ => throw new InvalidEnumArgumentException()
        };
    }

    internal static List<RecipientFileTransferStatusDetailsExt> MapToRecipients(List<ActorFileTransferStatusEntity> recipientEvents)
    {
        var lastStatusForEveryRecipient = recipientEvents
            .GroupBy(receipt => receipt.Actor.ActorExternalId)
            .Select(receiptsForRecipient =>
                receiptsForRecipient.MaxBy(receipt => receipt.Date))
            .Where(receipt => receipt is not null)
            .ToList().OfType<ActorFileTransferStatusEntity>();
        return lastStatusForEveryRecipient.Select(statusEvent => new RecipientFileTransferStatusDetailsExt()
        {
            Recipient = statusEvent.Actor.ActorExternalId,
            CurrentRecipientFileTransferStatusChanged = statusEvent.Date,
            CurrentRecipientFileTransferStatusCode = MapToExternalRecipientStatus(statusEvent.Status),
            CurrentRecipientFileTransferStatusText = MapToRecipientStatusText(statusEvent.Status)
        }).ToList();
    }

    internal static RecipientFileTransferStatusExt MapToExternalRecipientStatus(ActorFileTransferStatus actorFileTransferStatus)
    {
        return actorFileTransferStatus switch
        {
            ActorFileTransferStatus.Initialized => RecipientFileTransferStatusExt.Initialized,
            ActorFileTransferStatus.DownloadStarted => RecipientFileTransferStatusExt.DownloadStarted,
            ActorFileTransferStatus.DownloadConfirmed => RecipientFileTransferStatusExt.DownloadConfirmed,
            _ => throw new InvalidEnumArgumentException()
        };
    }

    internal static string MapToRecipientStatusText(ActorFileTransferStatus actorFileTransferStatus)
    {
        return actorFileTransferStatus switch
        {
            ActorFileTransferStatus.Initialized => "Initialized",
            ActorFileTransferStatus.DownloadStarted => "Recipient has attempted to download fileTransfer",
            ActorFileTransferStatus.DownloadConfirmed => "Recipient has downloaded fileTransfer",
            _ => throw new InvalidEnumArgumentException()
        };

    }
}
