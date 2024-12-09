using System.Text.Json.Serialization;

namespace Altinn.Broker.Common.Helpers.Models;
public class TokenConsumer
{
    [JsonPropertyName("authority")]
    public string Authority { get; set; }

    [JsonPropertyName("ID")]
    public string ID { get; set; }
}
