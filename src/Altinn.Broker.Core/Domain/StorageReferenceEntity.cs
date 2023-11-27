public class StorageReferenceEntity
{
    public long Id { get; set; }
    public StorageProviderEntity StorageProvider { get; set; }
    public string FileLocation { get; set; }
}
