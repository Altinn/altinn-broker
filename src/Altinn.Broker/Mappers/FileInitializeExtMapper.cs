


using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;

public static class FileInitializeExtMapper
{

    public static FileEntity MapToDomain(FileInitalizeExt initializeExt, string caller)
    {
        return new FileEntity()
        {
            FileId = Guid.Empty,
            ApplicationId = caller,
            Filename = initializeExt.FileName,
            ExternalFileReference = initializeExt.SendersFileReference,
            Checksum = initializeExt.Checksum,
            Sender = initializeExt.Sender,
            ActorEvents = (initializeExt.Recipients.Concat(new[] { initializeExt.Sender })).Select(actorExternalId => new ActorFileStatusEntity()
            {
                FileId = Guid.Empty,
                Actor = new ActorEntity()
                {
                    ActorExternalId = actorExternalId,
                    ActorId = 0
                },
                Status = Altinn.Broker.Core.Domain.Enums.ActorFileStatus.Initialized
            }).ToList(),
            PropertyList = initializeExt.Metadata
        };
    }
}