
using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IServiceRepository
{
    Task<ServiceEntity?> GetService(long id);
    Task<ServiceEntity?> GetService(string clientId);
    Task<List<string>> SearchServices(string serviceOwnerOrgNo);
    Task<long> InitializeService(string serviceOwnerId, string organizationNumber, string clientId);
}
