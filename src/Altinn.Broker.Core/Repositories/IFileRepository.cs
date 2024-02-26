using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Repositories;

public interface IFileRepository
{
    Task<Guid> AddFile(
        ResourceOwnerEntity resourceOwner,
        ResourceEntity service,
        string filename,
        string sendersFileReference,
        string senderExternalId,
        List<string> recipientIds,
        Dictionary<string, string> propertyList,
        string? checksum,
        long? filesize,
        CancellationToken cancellationToken);
    Task<Domain.FileEntity?> GetFile(Guid fileId, CancellationToken cancellationToken);
    Task<List<Guid>> GetFilesAssociatedWithActor(FileSearchEntity fileSearch, CancellationToken cancellationToken);
    Task<List<Guid>> GetFilesForRecipientWithRecipientStatus(FileSearchEntity fileSearch, CancellationToken cancellationToken);
    Task<List<Guid>> LegacyGetFilesForRecipientsWithRecipientStatus(LegacyFileSearchEntity fileSearch, CancellationToken cancellationToken);
    Task SetChecksum(Guid fileId, string checksum, CancellationToken cancellationToken);
    Task SetStorageDetails(
        Guid fileId,
        long storageProviderId,
        string fileLocation,
        long fileSize,
        CancellationToken cancellationToken
    );
}
