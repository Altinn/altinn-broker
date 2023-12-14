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
    Task<List<Guid>> GetFilesAssociatedWithActor(FileSearchEntity fileSearch);
    Task SetStorageReference(
        Guid fileId,
        long storageProviderId,
        string fileLocation
    );
}
