namespace Altinn.Broker.Core.Domain;

public class ResourceOwnerEntity
{
    public string Id { get; set; }
    public string Name { get; set; }
    public StorageProviderEntity? StorageProvider { get; set; }
    public TimeSpan FileTimeToLive { get; set; }
    public Guid ResourceGroupName { get; set; }
}
