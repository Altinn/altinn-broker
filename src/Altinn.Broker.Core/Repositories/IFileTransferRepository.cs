using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Repositories;

public interface IFileTransferRepository
{
    Task<Guid> AddFileTransfer(
        ServiceOwnerEntity serviceOwner,
        ResourceEntity service,
        string fileName,
        string sendersFileTransferReference,
        string senderExternalId,
        List<string> recipientIds,
        Dictionary<string, string> propertyList,
        string? checksum,
        long? fileTransferSize,
        string? hangfireJobId,
        CancellationToken cancellationToken);
    Task<Domain.FileTransferEntity?> GetFileTransfer(Guid fileTransferId, CancellationToken cancellationToken);
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
    Task<List<FileTransferEntity>> GetNonDeletedFileTransfersByStorageProvider(
        long storageProviderId,
        CancellationToken cancellationToken
    );
    Task SetFileTransferHangfireJobId(Guid fileTransferId, string hangfireJobId, CancellationToken cancellationToken);
}
