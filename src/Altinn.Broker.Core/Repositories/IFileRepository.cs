using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;

public interface IFileRepository
{
    void AddFile(Core.Domain.File file);
    void AddReceipt(FileReceipt receipt);
    Core.Domain.File? GetFile(Guid fileId);
}