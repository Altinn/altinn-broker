using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Repositories;
public interface IFileTransferStatusRepository
{
    Task<List<FileTransferStatusEntity>> GetFileTransferStatusHistory(Guid fileTransferId, CancellationToken cancellationToken);
    Task InsertFileTransferStatus(Guid fileTransferId, FileTransferStatus status, string? detailedFileTransferStatus = null, DateTimeOffset? timestamp = null, CancellationToken cancellationToken = default);
    Task<List<FileTransferStatusEntity>> GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(List<FileTransferStatus> statusFilters, DateTime maxStatusAge, CancellationToken cancellationToken);
}
