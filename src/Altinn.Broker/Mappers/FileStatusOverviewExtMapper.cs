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
            Metadata = file.Metadata,
            Recipients = MapToRecipients(file.ActorEvents, file.Sender, file.ApplicationId),
            SendersFileReference = file.ExternalFileReference
        };
    }

    public static FileStatusExt MapToExternalEnum(FileStatus domainEnum)
    {
        return domainEnum switch
        {
            FileStatus.Initialized => FileStatusExt.Initialized,
            FileStatus.AwaitingUpload => FileStatusExt.Initialized,
            FileStatus.UploadInProgress => FileStatusExt.UploadInProgress,
            FileStatus.AwaitingUploadProcessing => FileStatusExt.AwaitingUploadProcessing,
            FileStatus.UploadedAndProcessed => FileStatusExt.UploadedAndProcessed,
            FileStatus.Published => FileStatusExt.Published,
            FileStatus.Cancelled => FileStatusExt.Cancelled,
            FileStatus.Downloaded => FileStatusExt.Published,
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
            FileStatus.AwaitingUpload => "Ready for upload",
            FileStatus.UploadInProgress => "Uploading",
            FileStatus.AwaitingUploadProcessing => "Processing upload",
            FileStatus.UploadedAndProcessed => "Uploaded",
            FileStatus.Published => "Ready for download",
            FileStatus.Cancelled => "File cancelled",
            FileStatus.Downloaded => "Ready for download",
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

    public static List<RecipientFileStatusEventExt> MapToRecipients(List<ActorFileStatusEntity> actorEvents, string sender, string applicationId)
    {
        var recipientEvents = actorEvents.Where(actorEvent => actorEvent.Actor.ActorExternalId != sender);
        var lastStatusForEveryRecipient = recipientEvents
            .GroupBy(receipt => receipt.Actor.ActorExternalId)
            .Select(receiptsForRecipient =>
                receiptsForRecipient.MaxBy(receipt => receipt.Date))
            .ToList();
        return lastStatusForEveryRecipient.Select(statusEvent => new RecipientFileStatusEventExt()
        {
            Recipient = statusEvent.Actor.ActorExternalId,
            RecipientFileStatusChanged = statusEvent.Date,
            RecipientFileStatusCode = MapToExternalRecipientStatus(statusEvent.Status),
            RecipientFileStatusText = MapToRecipientStatusText(statusEvent.Status)
        }).ToList();
    }

    private static RecipientFileStatusExt MapToExternalRecipientStatus(Altinn.Broker.Core.Domain.Enums.ActorFileStatus actorFileStatus)
    {
        return actorFileStatus switch
        {
            Altinn.Broker.Core.Domain.Enums.ActorFileStatus.None => RecipientFileStatusExt.Initialized,
            Altinn.Broker.Core.Domain.Enums.ActorFileStatus.Initialized => RecipientFileStatusExt.Initialized,
            Altinn.Broker.Core.Domain.Enums.ActorFileStatus.Uploaded => RecipientFileStatusExt.Published,
            Altinn.Broker.Core.Domain.Enums.ActorFileStatus.Downloaded => RecipientFileStatusExt.ConfirmDownloaded
        };
    }

    private static string MapToRecipientStatusText(Altinn.Broker.Core.Domain.Enums.ActorFileStatus actorFileStatus)
    {
        return actorFileStatus switch
        {
            Altinn.Broker.Core.Domain.Enums.ActorFileStatus.None => "Unknown",
            Altinn.Broker.Core.Domain.Enums.ActorFileStatus.Initialized => "Initialized",
            Altinn.Broker.Core.Domain.Enums.ActorFileStatus.Uploaded => "Sender has uploaded file",
            Altinn.Broker.Core.Domain.Enums.ActorFileStatus.Downloaded => "Recipient has downloaded file"
        };

    }
}