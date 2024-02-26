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
        CancellationToken ct);
    Task<Domain.FileEntity?> GetFile(Guid fileId, CancellationToken ct);
    Task<List<Guid>> GetFilesAssociatedWithActor(FileSearchEntity fileSearch, CancellationToken ct);
    Task<List<Guid>> GetFilesForRecipientWithRecipientStatus(FileSearchEntity fileSearch, CancellationToken ct);
    Task<List<Guid>> LegacyGetFilesForRecipientsWithRecipientStatus(LegacyFileSearchEntity fileSearch, CancellationToken ct);
    Task SetChecksum(Guid fileId, string checksum, CancellationToken ct);
    Task SetStorageDetails(
        Guid fileId,
        long storageProviderId,
        string fileLocation,
        long fileSize,
        CancellationToken ct
    );
}
