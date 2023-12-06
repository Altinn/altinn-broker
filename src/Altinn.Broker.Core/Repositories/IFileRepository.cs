﻿using Altinn.Broker.Core.Domain;
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

    Task AddReceiptAsync(ActorFileStatusEntity receipt);
    Task<Domain.FileEntity?> GetFileAsync(Guid fileId);
    Task<List<Guid>> GetFilesAvailableForCaller(string actorExernalReference);

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
    Task<List<FileStatusEntity>> GetFileStatusHistoryAsync(Guid fileId);
    Task<List<ActorFileStatusEntity>> GetActorEvents(Guid fileId);
    Task InsertFileStatus(Guid fileId, FileStatus status);
}
