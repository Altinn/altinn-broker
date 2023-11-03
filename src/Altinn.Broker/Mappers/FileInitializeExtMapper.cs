


using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;

public static class FileInitializeExtMapper
{

    public static FileEntity MapToDomain(FileInitalizeExt initializeExt, string caller)
    {
        return new FileEntity()
        {
            FileId = Guid.Empty,
            Filename = initializeExt.FileName,
            ExternalFileReference = initializeExt.SendersFileReference,
            Checksum = initializeExt.Checksum,
            Sender = caller,
            ActorEvents = initializeExt.Recipients.Select(recipient => new ActorFileStatusEntity()
            {
                FileId = Guid.Empty,
                Actor = new ActorEntity()
                {
                    ActorExternalId = recipient,
                    ActorId = 0
                }
            }).ToList(),
            Metadata = initializeExt.Metadata
        };
    }
}