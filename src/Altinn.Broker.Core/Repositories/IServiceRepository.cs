
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IServiceRepository
{
    Task<ServiceEntity?> GetService(long id);
    Task<ServiceEntity?> GetService(string organizationNumber);
    Task<long> InitializeService(string serviceOwnerId, string organizationNumber);
}
