using System.Text.Json.Serialization;

namespace Altinn.Broker.Integrations.Maskinporten.Models;

public class MaskinportenConsumer
{
    [JsonConstructor]
    public MaskinportenConsumer(
        string authority,
        string id
    )
    {
        Authority = authority;
        ID = id;
    }

    [JsonPropertyName("authority")]
    public string Authority { get; }

    [JsonPropertyName("ID")]
    public string ID { get; }
}
