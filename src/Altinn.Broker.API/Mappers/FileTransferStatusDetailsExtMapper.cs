using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class FileTransferStatusDetailsExtMapper
{
    internal static FileTransferStatusDetailsExt MapToExternalModel(FileTransferEntity fileTransfer, List<FileTransferStatusEntity> fileTransferEvents, List<ActorFileTransferStatusEntity> actorEvents)
    {
        var details = new FileTransferStatusDetailsExt();
        
        FileTransferStatusOverviewExtMapper.MapBaseProperties(fileTransfer, fileTransferEvents, details);
        
        details.FileTransferStatusHistory = MapToFileTransferStatusHistoryExt(fileTransferEvents);
        details.RecipientFileTransferStatusHistory = MapToRecipientEvents(actorEvents);
        
        return details;
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
