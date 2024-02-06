﻿using Altinn.Broker.Core.Domain;
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
        long? filesize);
    Task<Domain.FileEntity?> GetFile(Guid fileId);
    Task<List<Guid>> GetFilesAssociatedWithActor(FileSearchEntity fileSearch);
    Task<List<Guid>> GetFilesForRecipientWithRecipientStatus(FileSearchEntity fileSearch);
    Task<List<Guid>> LegacyGetFilesForRecipientsWithRecipientStatus(LegacyFileSearchEntity fileSearch);
    Task SetChecksum(Guid fileId, string checksum);
    Task SetStorageDetails(
        Guid fileId,
        long storageProviderId,
        string fileLocation,
        long fileSize
    );
}
