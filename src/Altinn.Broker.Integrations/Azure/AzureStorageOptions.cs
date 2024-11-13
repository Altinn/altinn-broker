namespace Altinn.Broker.Core.Options;

public class AzureStorageOptions
{
    public int BlockSize { get; set; }
    public int ConcurrentUploadThreads { get; set; }
    public int BlocksBeforeCommit { get; set; }
}
