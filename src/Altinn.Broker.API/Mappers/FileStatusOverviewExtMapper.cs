using System.ComponentModel;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class FileTransferStatusOverviewExtMapper
{
    internal static FileTransferOverviewExt MapToExternalModel(FileTransferEntity fileTransfer)
    {
        return new FileTransferOverviewExt()
        {
            Checksum = fileTransfer.Checksum,
            ResourceId = fileTransfer.ResourceId,
            FileTransferSize = fileTransfer.FileTransferSize,
            FileTransferId = fileTransfer.FileTransferId,
            FileName = fileTransfer.FileName,
            FileTransferStatus = MapToExternalEnum(fileTransfer.FileTransferStatusEntity.Status),
            Sender = fileTransfer.Sender.ActorExternalId,
            FileTransferStatusChanged = fileTransfer.FileTransferStatusChanged,
            FileTransferStatusText = MapToFileTransferStatusText(fileTransfer.FileTransferStatusEntity),
            PropertyList = fileTransfer.PropertyList,
            Recipients = MapToRecipients(fileTransfer.RecipientCurrentStatuses),
            SendersFileTransferReference = fileTransfer.SendersFileTransferReference,
            Created = fileTransfer.Created,
            ExpirationTime = fileTransfer.ExpirationTime
        };
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
            FileTransferStatus.Deleted => FileTransferStatusExt.Deleted,
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
            FileTransferStatus.Deleted => "FileTransfer has been deleted",
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
            .ToList();
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
