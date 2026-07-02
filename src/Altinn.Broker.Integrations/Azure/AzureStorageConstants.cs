namespace Altinn.Broker.Integrations.Azure;

public static class AzureStorageConstants
{
    public const string BrokerFilesContainerName = "brokerfiles";
    public const string TusStagingBlobPath = "tus-staging";
    public const string TusBlockStagingBlobPath = "tus-block-staging";
    public const string TusMd5ChecksumMetadataKey = "MD5Checksum";
}
