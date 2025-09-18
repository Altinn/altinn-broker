using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IFileTransferRepository
{
    Task<Guid> AddFileTransfer(
        ResourceEntity service,
        StorageProviderEntity storageProviderEntity,
        string fileName,
        string sendersFileTransferReference,
        string senderExternalId,
        List<string> recipientIds,
        DateTimeOffset expirationTime,
        Dictionary<string, string> propertyList,
        string? checksum,
        bool useVirusScan,
        CancellationToken cancellationToken);
    Task<Domain.FileTransferEntity?> GetFileTransfer(Guid fileTransferId, CancellationToken cancellationToken);
    Task<List<FileTransferEntity>> GetFileTransfers(List<Guid> fileTransferIds, CancellationToken cancellationToken);
    Task<List<Guid>> GetFileTransfersAssociatedWithActor(FileTransferSearchEntity fileTransferSearch, CancellationToken cancellationToken);
    Task<List<Guid>> GetFileTransfersForRecipientWithRecipientStatus(FileTransferSearchEntity fileTransferSearch, CancellationToken cancellationToken);
    Task<List<Guid>> LegacyGetFilesForRecipientsWithRecipientStatus(LegacyFileSearchEntity fileTransferSearch, CancellationToken cancellationToken);
    Task SetChecksum(Guid fileTransferId, string checksum, CancellationToken cancellationToken);
    Task SetStorageDetails(
        Guid fileTransferId,
        long storageProviderId,
        string fileLocation,
        long fileTransferSize,
        CancellationToken cancellationToken
    );
    Task SetFileTransferHangfireJobId(Guid fileTransferId, string hangfireJobId, CancellationToken cancellationToken);
}
