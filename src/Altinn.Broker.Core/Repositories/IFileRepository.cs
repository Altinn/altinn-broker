using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IFileRepository
{
    Task<Guid> AddFileAsync(Core.Domain.File file);
    Task AddReceiptAsync(FileReceipt receipt);
    Task<Domain.File?> GetFileAsync(Guid fileId);
    Task<List<string>> GetFilesAsync(string actorExernalReference);
}