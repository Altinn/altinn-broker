public class StorageProviderEntity
{
    public long Id { get; set; }
    public DateTimeOffset Created { get; set; }

    public StorageProviderType Type { get; set; }

    public string ResourceName { get; set; }
}
