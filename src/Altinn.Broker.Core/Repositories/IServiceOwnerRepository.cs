using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IServiceOwnerRepository
{
    Task InitializeServiceOwner(string sub, string name);
    Task<ServiceOwnerEntity?> GetServiceOwner(string sub);
    Task InitializeStorageProvider(string sub, string resourceName, StorageProviderType storageType);
}
