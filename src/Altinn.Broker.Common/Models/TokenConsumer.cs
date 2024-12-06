using System.Text.Json.Serialization;

namespace Altinn.Broker.Core.Helpers.Models;
public class TokenConsumer
{
    [JsonPropertyName("authority")]
    public string Authority { get; set; }

    [JsonPropertyName("ID")]
    public string ID { get; set; }
}
