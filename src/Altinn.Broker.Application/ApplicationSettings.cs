namespace Altinn.Broker.Application.Settings;

public class ApplicationSettings
{
    public long MaxFileUploadSize { get; set; } = int.MaxValue;
    public string DefaultGracePeriod { get; set; } = "PT2H";
    public string MaxGracePeriod { get; set; } = "PT24H";
}
