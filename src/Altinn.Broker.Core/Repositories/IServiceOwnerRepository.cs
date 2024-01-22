using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IResourceOwnerRepository
{
    Task InitializeResourceOwner(string sub, string name, TimeSpan fileTimeToLive);
    Task<ResourceOwnerEntity?> GetResourceOwner(string sub);
    Task InitializeStorageProvider(string sub, string resourceName, StorageProviderType storageType);
}
