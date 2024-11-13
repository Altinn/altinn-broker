namespace Altinn.Broker.Application.Settings;

public static class ApplicationConstants
{
    public const long MaxFileUploadSize = 100L * 1024 * 1024 * 1024;
    public const long MaxVirusScanUploadSize = 2L * 1024 * 1024 * 1024;
    public const string DefaultGracePeriod = "PT2H";
    public const string MaxGracePeriod = "PT24H";
}
