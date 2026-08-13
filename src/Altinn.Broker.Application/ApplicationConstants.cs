namespace Altinn.Broker.Application.Settings;

public static class ApplicationConstants
{
    // For the maximum transfer size, see AzureStorageOptions.MaxTotalTransferBytes.
    public const long MaxVirusScanUploadSize = 50L * 1000 * 1000 * 1000;
    public const string DefaultGracePeriod = "PT2H";
    public const string MaxGracePeriod = "PT24H";
}
