namespace Altinn.Broker.Core.Options;

public class GeneralSettings
{
    public string SlackUrl { get; set; } = string.Empty;
    public string ApplicationInsightsConnectionString { get; set; } = string.Empty;
    public bool SimulateMalwareScan { get; set; } = false;

    /// <summary>
    /// Broker's own externally reachable base URL, for example "https://platform.tt02.altinn.no". Used to build the
    /// condition endpoint Altinn Notification calls back on before sending a notification. Notifications are not given
    /// a condition endpoint when this is unset or points at localhost.
    /// </summary>
    public string BrokerBaseUrl { get; set; } = string.Empty;
}
