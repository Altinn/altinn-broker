using Microsoft.Extensions.Hosting;

namespace Altinn.Broker.Core.Options;

public class SlackSettings
{
    public string NotificationChannel { get; }

    public SlackSettings(IHostEnvironment hostEnvironment)
    {
        NotificationChannel = hostEnvironment.IsDevelopment() ? "#mf-varsling-critical" : "#test-varslinger";
    }
} 