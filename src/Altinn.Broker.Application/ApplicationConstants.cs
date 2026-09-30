namespace Altinn.Broker.Application.Settings;

public static class ApplicationConstants
{
    public const long MaxFileUploadSize = 32L * 50000 * 1024 * 1024;
    public const long MaxVirusScanUploadSize = 50L * 1000 * 1000 * 1000;
    public const string DefaultGracePeriod = "PT2H";
    public const string MaxGracePeriod = "PT24H";

    /// <summary>
    /// Gatekeeper resource for BrokerBox configuration: ID-porten callers must have
    /// <c>publish</c> on this resource for the selected service-owner party.
    /// </summary>
    public const string BrokerBoxConfigureGatekeeperResourceId = "digdir-broker-administrasjon";
}
