using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IFileRepository
{
    Task AddFileAsync(Core.Domain.File file);
    Task AddReceiptAsync(FileReceipt receipt);
    Task<Domain.File?> GetFileAsync(Guid fileId);
}
