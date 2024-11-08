using Microsoft.Extensions.Hosting;

namespace Altinn.Broker.Core.Domain;

public class ServiceOwnerEntity
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public List<StorageProviderEntity> StorageProviders { get; set; }
    public StorageProviderEntity? StorageProvider { get; set; }

    public StorageProviderEntity? GetStorageProvider(bool withVirusScan)
    {
        if (withVirusScan)
        {
            return StorageProviders.FirstOrDefault(sp => sp.Type == StorageProviderType.Altinn3Azure);
        } 
        else
        {
            return StorageProviders.FirstOrDefault(sp => sp.Type == StorageProviderType.Altinn3AzureWithoutVirusScan);
        }
    }
}
