using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

public static class FileStatusOverviewExtMapper
{
    public static FileOverviewExt MapToExternalModel(FileEntity file)
    {
        return new FileOverviewExt()
        {
            Checksum = file.Checksum,
            FileId = file.FileId,
            FileName = file.Filename,
            FileStatus = MapToExternalEnum(file.FileStatus),
            Sender = file.Sender,
            FileStatusChanged = file.FileStatusChanged,
            FileStatusText = MapToFileStatusText(file.FileStatus),
            PropertyList = file.PropertyList,
            Recipients = MapToRecipients(file.ActorEvents, file.Sender, file.ApplicationId),
            SendersFileReference = file.SendersFileReference
        };
    }

    public static FileStatusExt MapToExternalEnum(FileStatus domainEnum)
    {
        return domainEnum switch
        {
            FileStatus.Initialized => FileStatusExt.Initialized,
            FileStatus.UploadStarted => FileStatusExt.UploadStarted,
            FileStatus.UploadProcessing => FileStatusExt.UploadProcessing,
            FileStatus.Published => FileStatusExt.Published,
            FileStatus.Cancelled => FileStatusExt.Cancelled,
            FileStatus.AllConfirmedDownloaded => FileStatusExt.AllConfirmedDownloaded,
            FileStatus.Deleted => FileStatusExt.Deleted,
            FileStatus.Failed => FileStatusExt.Failed
        };
    }

    public static string MapToFileStatusText(FileStatus domainEnum)
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
            FileStatus.Failed => "Upload failed"
        };
    }

    public static List<FileStatusEventExt> MapToFileStatusHistoryExt(List<FileStatusEntity> fileHistory) => fileHistory.Select(entity => new FileStatusEventExt()
    {
        FileStatus = MapToExternalEnum(entity.Status),
        FileStatusChanged = entity.Date,
        FileStatusText = MapToFileStatusText(entity.Status)
    }).ToList();

    public static List<RecipientFileStatusDetailsExt> MapToRecipients(List<ActorFileStatusEntity> actorEvents, string sender, string applicationId)
    {
        var recipientEvents = actorEvents.Where(actorEvent => actorEvent.Actor.ActorExternalId != sender);
        var lastStatusForEveryRecipient = recipientEvents
            .GroupBy(receipt => receipt.Actor.ActorExternalId)
            .Select(receiptsForRecipient =>
                receiptsForRecipient.MaxBy(receipt => receipt.Date))
            .ToList();
        return lastStatusForEveryRecipient.Select(statusEvent => new RecipientFileStatusDetailsExt()
        {
            Recipient = statusEvent.Actor.ActorExternalId,
            CurrentRecipientFileStatusChanged = statusEvent.Date,
            CurrentRecipientFileStatusCode = MapToExternalRecipientStatus(statusEvent.Status),
            CurrentRecipientFileStatusText = MapToRecipientStatusText(statusEvent.Status)
        }).ToList();
    }

    private static RecipientFileStatusExt MapToExternalRecipientStatus(ActorFileStatus actorFileStatus)
    {
        return actorFileStatus switch
        {
            ActorFileStatus.None => RecipientFileStatusExt.Initialized,
            ActorFileStatus.Initialized => RecipientFileStatusExt.Initialized,
            ActorFileStatus.DownloadStarted => RecipientFileStatusExt.DownloadStarted,
            ActorFileStatus.DownloadConfirmed => RecipientFileStatusExt.DownloadConfirmed
        };
    }

    private static string MapToRecipientStatusText(ActorFileStatus actorFileStatus)
    {
        return actorFileStatus switch
        {
            ActorFileStatus.None => "Unknown",
            ActorFileStatus.Initialized => "Initialized",
            ActorFileStatus.DownloadStarted => "Recipient has attempted to download file",
            ActorFileStatus.DownloadConfirmed => "Recipient has downloaded file"
        };

    }

    internal static List<RecipientFileStatusEventExt> MapToRecipientEvents(List<ActorFileStatusEntity> actorEvents)
    {
        return actorEvents.Select(actorEvent => new RecipientFileStatusEventExt()
        {
            Recipient = actorEvent.Actor.ActorExternalId,
            RecipientFileStatusChanged = actorEvent.Date,
            RecipientFileStatusCode = MapToExternalRecipientStatus(actorEvent.Status),
            RecipientFileStatusText = MapToRecipientStatusText(actorEvent.Status)
        }).ToList();
    }
}
