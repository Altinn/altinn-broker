using Microsoft.Extensions.Hosting;

namespace Altinn.Broker.Core.Options;

public class SlackSettings
{
    private readonly IHostEnvironment _hostEnvironment;

    public SlackSettings(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    public string NotificationChannel => _hostEnvironment.IsProduction() ? "#mf-varsling-critical" : "#test-varslinger";
} 