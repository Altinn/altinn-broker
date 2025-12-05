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
    Task<IReadOnlyList<FileTransferEntity>> GetFileTransfers(IReadOnlyCollection<Guid> fileTransferIds, CancellationToken cancellationToken);
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
    Task<(List<FileTransferEntity> FileTransfers, Dictionary<Guid, string> ServiceOwnerIds)> GetFileTransfersForReportWithServiceOwnerIds(CancellationToken cancellationToken);

    Task<List<Guid>> GetFileTransfersByResourceId(string resourceId, CancellationToken cancellationToken);
    Task<List<Guid>> GetFileTransfersByPropertyTag(string resourceId, string propertyKey, string propertyValue, CancellationToken cancellationToken);
    Task<int> HardDeleteFileTransfersByIds(IEnumerable<Guid> fileTransferIds, CancellationToken cancellationToken);
    Task<List<AggregatedDailySummaryData>> GetAggregatedDailySummaryData(CancellationToken cancellationToken);
}

public class AggregatedDailySummaryData
{
    public DateTime Date { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public string ServiceOwnerId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public int RecipientType { get; set; }
    public int AltinnVersion { get; set; }
    public int MessageCount { get; set; }
    public long DatabaseStorageBytes { get; set; }
    public long AttachmentStorageBytes { get; set; }
}
