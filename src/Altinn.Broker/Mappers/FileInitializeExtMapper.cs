


using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;
using Altinn.Broker.Validators;

public static class FileInitializeExtMapper {

    public static Altinn.Broker.Core.Domain.File MapToDomain(FileInitalizeExt initializeExt, string caller) 
    {
        return new Altinn.Broker.Core.Domain.File(){
            FileId = Guid.Empty,
            Filename = initializeExt.FileName,
            ExternalFileReference = initializeExt.SendersFileReference,
            Checksum = initializeExt.Checksum,
            Sender = caller,
            ActorEvents = initializeExt.Recipients.Select(recipient => new ActorFileStatus(){
                FileId = Guid.Empty,
                Actor = new Actor(){
                    ActorExternalId = recipient,
                    ActorId = 0
                }
            }).ToList(),
            Metadata = initializeExt.Metadata
        };
    }
}