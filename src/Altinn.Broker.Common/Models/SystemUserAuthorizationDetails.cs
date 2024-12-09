using System.Text.Json.Serialization;

namespace Altinn.Broker.Common.Helpers.Models;
public class SystemUserAuthorizationDetails
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("systemuser_id")]
    public required List<string> SystemUserId { get; set; }

    [JsonPropertyName("systemuser_org")]
    public required SystemUserOrg SystemUserOrg { get; set; }

    [JsonPropertyName("system_id")]
    public required string SystemId { get; set; }
}

public class SystemUserAuthorization
{
    [JsonPropertyName("authorization_details")]
    public List<SystemUserAuthorizationDetails> AuthorizationDetails { get; set; }
}

public class SystemUserOrg
{
    [JsonPropertyName("authority")]
    public required string Authority { get; set; }

    [JsonPropertyName("ID")]
    public required string ID { get; set; }
}
