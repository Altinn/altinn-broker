using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Repositories;
public interface IServiceOwnerRepository
{
    Task InitializeServiceOwner(string sub, string name);

    /// <summary>
    /// Inserts the service owner as <see cref="ProvisioningPhase.Queued"/>.
    /// Returns false when the organization is already registered.
    /// </summary>
    Task<bool> TryInitializeServiceOwner(string sub, string name);

    Task<ServiceOwnerEntity?> GetServiceOwner(string sub);
    Task SetProvisioningPhase(string serviceOwnerId, ProvisioningPhase phase);
    Task InitializeStorageProvider(string sub, string resourceName, StorageProviderType storageType);

    /// <summary>
    /// Development stand-in for Azure deployment: one dummy storage account, then <see cref="ProvisioningPhase.Ready"/>.
    /// </summary>
    Task InitializeDevelopmentStorage(string serviceOwnerId);
}
