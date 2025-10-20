using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class FileTransferStatusDetailsExtMapper
{
    internal static FileTransferStatusDetailsExt MapToExternalModel(FileTransferEntity fileTransfer, List<FileTransferStatusEntity> fileTransferEvents, List<ActorFileTransferStatusEntity> actorEvents)
    {
        var fileTransferOverview = FileTransferStatusOverviewExtMapper.MapToExternalModel(fileTransfer);
        return new FileTransferStatusDetailsExt()
        {
            FileTransferId = fileTransfer.FileTransferId,
            ResourceId = fileTransfer.ResourceId,
            Checksum = fileTransferOverview.Checksum,
            UseVirusScan = fileTransferOverview.UseVirusScan,
            FileName = fileTransferOverview.FileName,
            Sender = fileTransferOverview.Sender,
            FileTransferStatus = fileTransferOverview.FileTransferStatus,
            FileTransferStatusChanged = fileTransferOverview.FileTransferStatusChanged,
            FileTransferStatusText = fileTransferOverview.FileTransferStatusText,
            PropertyList = fileTransferOverview.PropertyList,
            Recipients = fileTransferOverview.Recipients,
            SendersFileTransferReference = fileTransferOverview.SendersFileTransferReference,
            Created = fileTransfer.Created,
            ExpirationTime = fileTransferOverview.ExpirationTime,
            FileTransferStatusHistory = MapToFileTransferStatusHistoryExt(fileTransferEvents),
            RecipientFileTransferStatusHistory = MapToRecipientEvents(actorEvents)
        };

    }

    public static List<FileTransferStatusEventExt> MapToFileTransferStatusHistoryExt(List<FileTransferStatusEntity> fileTransferHistory) => fileTransferHistory.Select(entity => new FileTransferStatusEventExt()
    {
        FileTransferStatus = FileTransferStatusOverviewExtMapper.MapToExternalEnum(entity.Status),
        FileTransferStatusChanged = entity.Date,
        FileTransferStatusText = FileTransferStatusOverviewExtMapper.MapToFileTransferStatusText(entity)
    }).ToList();

    private static List<RecipientFileTransferStatusEventExt> MapToRecipientEvents(List<ActorFileTransferStatusEntity> actorEvents)
    {
        return actorEvents.Select(actorEvent => new RecipientFileTransferStatusEventExt()
        {
            Recipient = actorEvent.Actor.ActorExternalId,
            RecipientFileTransferStatusChanged = actorEvent.Date,
            RecipientFileTransferStatusCode = FileTransferStatusOverviewExtMapper.MapToExternalRecipientStatus(actorEvent.Status),
            RecipientFileTransferStatusText = FileTransferStatusOverviewExtMapper.MapToRecipientStatusText(actorEvent.Status)
        }).ToList();
    }
}
