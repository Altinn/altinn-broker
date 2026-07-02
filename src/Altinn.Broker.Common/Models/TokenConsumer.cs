using System.Text.Json.Serialization;

namespace Altinn.Broker.Common.Helpers.Models;
public class TokenConsumer
{
    [JsonPropertyName("authority")]
    public required string Authority { get; set; }

    [JsonPropertyName("ID")]
    public required string ID { get; set; }
}
