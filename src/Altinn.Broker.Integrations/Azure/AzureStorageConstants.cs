namespace Altinn.Broker.Integrations.Azure;

public static class AzureStorageConstants
{
    public const string BrokerFilesContainerName = "brokerfiles";
    /// <summary>Largest block Azure accepts in a single Put Block.</summary>
    public const long MaxBlockSizeBytes = 100L * 1000 * 1000; // 100 MB
    public const string TusStagingBlobPath = "tus-staging";
    public const string TusBlockStagingBlobPath = "tus-block-staging";
    public const string TusMd5ChecksumMetadataKey = "MD5Checksum";
    public const string TusUploadLengthMetadataKey = "UploadLength";
    public const string TusStripeSizeMetadataKey = "StripeSize";
    public const string TusStripeCountMetadataKey = "StripeCount";
}
