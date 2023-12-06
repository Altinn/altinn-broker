using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class FileStatusDetailsExtMapper
{
    internal static FileStatusDetailsExt MapToExternalModel(FileEntity file, List<FileStatusEntity> fileEvents, List<ActorFileStatusEntity> actorEvents)
    {
        var fileOverview = FileStatusOverviewExtMapper.MapToExternalModel(file);
        return new FileStatusDetailsExt()
        {
            Checksum = fileOverview.Checksum,
            FileId = file.FileId,
            FileName = fileOverview.FileName,
            Sender = fileOverview.Sender,
            FileStatus = fileOverview.FileStatus,
            FileStatusChanged = fileOverview.FileStatusChanged,
            FileStatusText = fileOverview.FileStatusText,
            PropertyList = fileOverview.PropertyList,
            Recipients = fileOverview.Recipients,
            SendersFileReference = fileOverview.SendersFileReference,
            FileStatusHistory = MapToFileStatusHistoryExt(fileEvents),
            RecipientFileStatusHistory = MapToRecipientEvents(actorEvents.Where(actorEvents => actorEvents.Actor.ActorExternalId != file.Sender).ToList())
        };

    }

    public static List<FileStatusEventExt> MapToFileStatusHistoryExt(List<FileStatusEntity> fileHistory) => fileHistory.Select(entity => new FileStatusEventExt()
    {
        FileStatus = FileStatusOverviewExtMapper.MapToExternalEnum(entity.Status),
        FileStatusChanged = entity.Date,
        FileStatusText = FileStatusOverviewExtMapper.MapToFileStatusText(entity.Status)
    }).ToList();

    private static List<RecipientFileStatusEventExt> MapToRecipientEvents(List<ActorFileStatusEntity> actorEvents)
    {
        return actorEvents.Select(actorEvent => new RecipientFileStatusEventExt()
        {
            Recipient = actorEvent.Actor.ActorExternalId,
            RecipientFileStatusChanged = actorEvent.Date,
            RecipientFileStatusCode = FileStatusOverviewExtMapper.MapToExternalRecipientStatus(actorEvent.Status),
            RecipientFileStatusText = FileStatusOverviewExtMapper.MapToRecipientStatusText(actorEvent.Status)
        }).ToList();
    }
}
