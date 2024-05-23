namespace Altinn.Broker.Core.Domain;

public class ServiceOwnerEntity
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public StorageProviderEntity? StorageProvider { get; set; }
}
