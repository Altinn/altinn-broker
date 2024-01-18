using System.ComponentModel;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class LegacyFileStatusOverviewExtMapper
{
    internal static LegacyFileOverviewExt MapToExternalModel(FileEntity file)
    {
        return new LegacyFileOverviewExt()
        {
            Checksum = file.Checksum,
            FileSize = file.FileSize,
            FileId = file.FileId,
            FileName = file.Filename,
            FileStatus = MapToExternalEnum(file.FileStatus),
            Sender = file.Sender.ActorExternalId,
            FileStatusChanged = file.FileStatusChanged,
            FileStatusText = MapToFileStatusText(file.FileStatus),
            PropertyList = file.PropertyList,
            Recipients = MapToRecipients(file.RecipientCurrentStatuses),
            SendersFileReference = file.SendersFileReference,
            Created = file.Created,
            ExpirationTime = file.ExpirationTime
        };
    }

    internal static LegacyFileStatusExt MapToExternalEnum(FileStatus domainEnum)
    {
        return domainEnum switch
        {
            FileStatus.Initialized => LegacyFileStatusExt.Initialized,
            FileStatus.UploadStarted => LegacyFileStatusExt.UploadStarted,
            FileStatus.UploadProcessing => LegacyFileStatusExt.UploadProcessing,
            FileStatus.Published => LegacyFileStatusExt.Published,
            FileStatus.Cancelled => LegacyFileStatusExt.Cancelled,
            FileStatus.AllConfirmedDownloaded => LegacyFileStatusExt.AllConfirmedDownloaded,
            FileStatus.Deleted => LegacyFileStatusExt.Deleted,
            FileStatus.Failed => LegacyFileStatusExt.Failed,
            _ => throw new InvalidEnumArgumentException()
        };
    }

    internal static string MapToFileStatusText(FileStatus domainEnum)
    {
        return domainEnum switch
        {
            FileStatus.Initialized => "Ready for upload",
            FileStatus.UploadStarted => "Upload started",
            FileStatus.UploadProcessing => "Processing upload",
            FileStatus.Published => "Ready for download",
            FileStatus.Cancelled => "File cancelled",
            FileStatus.AllConfirmedDownloaded => "All downloaded",
            FileStatus.Deleted => "File has been deleted",
            FileStatus.Failed => "Upload failed",
            _ => throw new InvalidEnumArgumentException()
        };
    }

    internal static List<LegacyRecipientFileStatusDetailsExt> MapToRecipients(List<ActorFileStatusEntity> recipientEvents)
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

    internal static LegacyRecipientFileStatusExt MapToExternalRecipientStatus(ActorFileStatus actorFileStatus)
    {
        return actorFileStatus switch
        {
            ActorFileStatus.Initialized => LegacyRecipientFileStatusExt.Initialized,
            ActorFileStatus.DownloadStarted => LegacyRecipientFileStatusExt.DownloadStarted,
            ActorFileStatus.DownloadConfirmed => LegacyRecipientFileStatusExt.DownloadConfirmed,
            _ => throw new InvalidEnumArgumentException()
        };
    }

    internal static string MapToRecipientStatusText(ActorFileStatus actorFileStatus)
    {
        return actorFileStatus switch
        {
            ActorFileStatus.Initialized => "Initialized",
            ActorFileStatus.DownloadStarted => "Recipient has attempted to download file",
            ActorFileStatus.DownloadConfirmed => "Recipient has downloaded file",
            _ => throw new InvalidEnumArgumentException()
        };

    }
}
