using System.Text.Json.Serialization;

namespace Altinn.Broker.Core.Helpers.Models;
public class SystemUserAuthorizationDetails
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("systemuser_id")]
    public List<string> SystemUserId { get; set; }

    [JsonPropertyName("systemuser_org")]
    public SystemUserOrg SystemUserOrg { get; set; }

    [JsonPropertyName("system_id")]
    public string SystemId { get; set; }
}

public class SystemUserAuthorization
{
    [JsonPropertyName("authorization_details")]
    public List<SystemUserAuthorizationDetails> AuthorizationDetails { get; set; }
}

public class SystemUserOrg
{
    [JsonPropertyName("authority")]
    public string Authority { get; set; }

    [JsonPropertyName("ID")]
    public string ID { get; set; }
}
