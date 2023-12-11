using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Repositories;
public interface IFileStatusRepository
{
    Task<List<FileStatusEntity>> GetFileStatusHistory(Guid fileId);
    Task InsertFileStatus(Guid fileId, FileStatus status);
}
