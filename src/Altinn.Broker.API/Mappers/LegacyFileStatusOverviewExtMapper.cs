using System.ComponentModel;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class LegacyFileStatusOverviewExtMapper
{
    internal static LegacyFileOverviewExt MapToExternalModel(FileTransferEntity fileTransfer)
    {
        return new LegacyFileOverviewExt()
        {
            Checksum = fileTransfer.Checksum,
            ResourceId = fileTransfer.ResourceId,
            FileSize = fileTransfer.FileTransferSize,
            FileId = fileTransfer.FileTransferId,
            FileName = fileTransfer.FileName,
            FileStatus = MapToExternalEnum(fileTransfer.FileTransferStatusEntity.Status),
            Sender = fileTransfer.Sender.ActorExternalId,
            FileStatusChanged = fileTransfer.FileTransferStatusChanged,
            FileStatusText = MapToFileStatusText(fileTransfer.FileTransferStatusEntity.Status),
            PropertyList = fileTransfer.PropertyList,
            Recipients = MapToRecipients(fileTransfer.RecipientCurrentStatuses),
            SendersFileReference = fileTransfer.SendersFileTransferReference,
            Created = fileTransfer.Created,
            ExpirationTime = fileTransfer.ExpirationTime
        };
    }

    internal static LegacyFileStatusExt MapToExternalEnum(FileTransferStatus domainEnum)
    {
        return domainEnum switch
        {
            FileTransferStatus.Initialized => LegacyFileStatusExt.Initialized,
            FileTransferStatus.UploadStarted => LegacyFileStatusExt.UploadStarted,
            FileTransferStatus.UploadProcessing => LegacyFileStatusExt.UploadProcessing,
            FileTransferStatus.Published => LegacyFileStatusExt.Published,
            FileTransferStatus.Cancelled => LegacyFileStatusExt.Cancelled,
            FileTransferStatus.AllConfirmedDownloaded => LegacyFileStatusExt.AllConfirmedDownloaded,
            FileTransferStatus.Deleted => LegacyFileStatusExt.Deleted,
            FileTransferStatus.Failed => LegacyFileStatusExt.Failed,
            _ => throw new InvalidEnumArgumentException()
        };
    }

    internal static string MapToFileStatusText(FileTransferStatus domainEnum)
    {
        return domainEnum switch
        {
            FileTransferStatus.Initialized => "Ready for upload",
            FileTransferStatus.UploadStarted => "Upload started",
            FileTransferStatus.UploadProcessing => "Processing upload",
            FileTransferStatus.Published => "Ready for download",
            FileTransferStatus.Cancelled => "File cancelled",
            FileTransferStatus.AllConfirmedDownloaded => "All downloaded",
            FileTransferStatus.Deleted => "File has been deleted",
            FileTransferStatus.Failed => "Upload failed",
            _ => throw new InvalidEnumArgumentException()
        };
    }

    internal static List<LegacyRecipientFileStatusDetailsExt> MapToRecipients(List<ActorFileTransferStatusEntity> recipientEvents)
    {
        var lastStatusForEveryRecipient = recipientEvents
            .GroupBy(receipt => receipt.Actor.ActorExternalId)
            .Select(receiptsForRecipient =>
                receiptsForRecipient.MaxBy(receipt => receipt.Date))
            .ToList();
        return lastStatusForEveryRecipient.Select(statusEvent => new LegacyRecipientFileStatusDetailsExt()
        {
            Recipient = statusEvent.Actor.ActorExternalId,
            CurrentRecipientFileStatusChanged = statusEvent.Date,
            CurrentRecipientFileStatusCode = MapToExternalRecipientStatus(statusEvent.Status),
            CurrentRecipientFileStatusText = MapToRecipientStatusText(statusEvent.Status)
        }).ToList();
    }

    internal static LegacyRecipientFileStatusExt MapToExternalRecipientStatus(ActorFileTransferStatus actorFileStatus)
    {
        return actorFileStatus switch
        {
            ActorFileTransferStatus.Initialized => LegacyRecipientFileStatusExt.Initialized,
            ActorFileTransferStatus.DownloadStarted => LegacyRecipientFileStatusExt.DownloadStarted,
            ActorFileTransferStatus.DownloadConfirmed => LegacyRecipientFileStatusExt.DownloadConfirmed,
            _ => throw new InvalidEnumArgumentException()
        };
    }

    internal static string MapToRecipientStatusText(ActorFileTransferStatus actorFileStatus)
    {
        return actorFileStatus switch
        {
            ActorFileTransferStatus.Initialized => "Initialized",
            ActorFileTransferStatus.DownloadStarted => "Recipient has attempted to download file",
            ActorFileTransferStatus.DownloadConfirmed => "Recipient has downloaded file",
            _ => throw new InvalidEnumArgumentException()
        };

    }
}
