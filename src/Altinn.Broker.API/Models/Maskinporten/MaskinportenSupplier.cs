using System.Text.Json.Serialization;

namespace Altinn.Broker.Models.Maskinporten;

[method: JsonConstructor]
public class MaskinportenSupplier(
    string authority,
    string id
    )
{
    [JsonPropertyName("authority")]
    public string Authority { get; } = authority;

    [JsonPropertyName("ID")]
    public string ID { get; } = id;
}

