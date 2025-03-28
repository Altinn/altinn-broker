using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Repositories;
public interface IFileTransferStatusRepository
{
    Task<List<FileTransferStatusEntity>> GetFileTransferStatusHistory(Guid fileTransferId, CancellationToken cancellationToken);
    Task InsertFileTransferStatus(Guid fileTransferId, FileTransferStatus status, string? detailedFileTransferStatus = null, CancellationToken cancellationToken = default);
    Task<List<FileTransferStatusEntity>> GetCurrentFileTransferStatusesOfStatusAndOlderThanDate(FileTransferStatus statusFilter, DateTime maxStatusAge, CancellationToken cancellationToken);
}
