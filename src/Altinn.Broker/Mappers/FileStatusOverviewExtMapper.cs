using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

public static class FileStatusOverviewExtMapper
{
    public static FileOverviewExt MapToExternalModel(Core.Domain.File file)
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
            Recipients = MapToRecipients(file.ActorEvents),
            SendersFileReference = file.ExternalFileReference
        };
    }

    public static FileStatusExt MapToExternalEnum(FileStatus domainEnum)
    {
        return domainEnum switch {
            FileStatus.None => FileStatusExt.Initialized,
            FileStatus.Initialized => FileStatusExt.Initialized,
            FileStatus.Processing => FileStatusExt.AwaitingUploadProcessing,
            FileStatus.Ready => FileStatusExt.Initialized,
            FileStatus.Failed => FileStatusExt.Failed,
            FileStatus.Deleted => FileStatusExt.Deleted,
        };
    }

    public static string MapToFileStatusText(FileStatus domainEnum)
    {
        return domainEnum switch
        {
            FileStatus.None => "Ready for upload",
            FileStatus.Initialized => "Ready for upload",
            FileStatus.Processing => "Processing upload",
            FileStatus.Ready => "Ready for download",
            FileStatus.Failed => "Upload failed",
            FileStatus.Deleted => "File has been deleted"
        };
    }

    public static List<FileStatusEventExt> MapToFileStatusHistoryExt(List<Core.Domain.FileStatusEntity> fileHistory) => fileHistory.Select(entity => new FileStatusEventExt()
    {
        FileStatus = MapToExternalEnum(entity.Status),
        FileStatusChanged = entity.Date,
        FileStatusText = MapToFileStatusText(entity.Status)
    }).ToList();

    public static List<RecipientFileStatusEventExt> MapToRecipients(List<Core.Domain.ActorFileStatus> fileReceipts)
    {
        var lastStatusForEveryRecipient = fileReceipts
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

    private static RecipientFileStatusExt MapToExternalRecipientStatus(Core.Domain.Enums.ActorFileStatus actorFileStatus) 
    {    
        return actorFileStatus switch {
            Core.Domain.Enums.ActorFileStatus.None => RecipientFileStatusExt.Initialized,
            Core.Domain.Enums.ActorFileStatus.Initialized => RecipientFileStatusExt.Initialized,
            Core.Domain.Enums.ActorFileStatus.Uploaded => RecipientFileStatusExt.Published,
            Core.Domain.Enums.ActorFileStatus.Downloaded => RecipientFileStatusExt.ConfirmDownloaded
        };
    }

    private static string MapToRecipientStatusText(Core.Domain.Enums.ActorFileStatus actorFileStatus)
    {
        return actorFileStatus switch
        {
            Core.Domain.Enums.ActorFileStatus.None => "Unknown",
            Core.Domain.Enums.ActorFileStatus.Initialized => "Waiting for file to be uploaded",
            Core.Domain.Enums.ActorFileStatus.Uploaded => "Sender has uploaded file",
            Core.Domain.Enums.ActorFileStatus.Downloaded => "Recipient has downloaded file"
        };

    }
}
