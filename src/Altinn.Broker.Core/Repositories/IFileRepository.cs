using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Repositories;

public interface IFileRepository
{
    Task<Guid> AddFile(
        ServiceOwnerEntity serviceOwner,
        string filename,
        string sendersFileReference,
        string senderExternalId,
        List<string> recipientIds,
        Dictionary<string, string> propertyList,
        string? checksum);
    Task<Domain.FileEntity?> GetFile(Guid fileId);
    Task<List<Guid>> GetFilesAssociatedWithActor(ActorEntity actor);
    Task AddReceipt(
        Guid fileId,
        Domain.Enums.ActorFileStatus status,
        string actorExternalReference
    );
    Task SetStorageReference(
        Guid fileId,
        long storageProviderId,
        string fileLocation
    );
    Task<List<FileStatusEntity>> GetFileStatusHistory(Guid fileId);
    Task<List<ActorFileStatusEntity>> GetActorEvents(Guid fileId);
    Task InsertFileStatus(Guid fileId, FileStatus status);
}
