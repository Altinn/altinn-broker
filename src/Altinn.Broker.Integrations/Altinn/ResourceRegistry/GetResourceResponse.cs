using System.Text.Json.Serialization;

namespace Altinn.Broker.Integrations.Altinn.ResourceRegistry;

internal class GetResourceResponse
{
    [JsonPropertyName("identifier")]
    public required string Identifier { get; set; }

    [JsonPropertyName("title")]
    public required Dictionary<string, string> Title { get; set; }

    [JsonPropertyName("description")]
    public required Dictionary<string, string> Description { get; set; }

    [JsonPropertyName("rightDescription")]
    public Dictionary<string, string>? RightDescription { get; set; }

    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("contactPoints")]
    public List<object>? ContactPoints { get; set; }

    [JsonPropertyName("isPartOf")]
    public string? IsPartOf { get; set; }

    [JsonPropertyName("resourceReferences")]
    public List<object>? ResourceReferences { get; set; }

    [JsonPropertyName("delegable")]
    public bool? Delegable { get; set; }

    [JsonPropertyName("visible")]
    public bool? Visible { get; set; }

    [JsonPropertyName("hasCompetentAuthority")]
    public required HasCompetentAuthority HasCompetentAuthority { get; set; }

    [JsonPropertyName("keywords")]
    public List<Keyword>? Keywords { get; set; }

    [JsonPropertyName("limitedByRRR")]
    public bool? LimitedByRRR { get; set; }

    [JsonPropertyName("selfIdentifiedUserEnabled")]
    public bool? SelfIdentifiedUserEnabled { get; set; }

    [JsonPropertyName("enterpriseUserEnabled")]
    public bool? EnterpriseUserEnabled { get; set; }

    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; set; }

    [JsonPropertyName("accessListMode")]
    public string? AccessListMode { get; set; }
}

internal class HasCompetentAuthority
{
    [JsonPropertyName("organization")]
    public required string Organization { get; set; }

    [JsonPropertyName("orgcode")]
    public required string Orgcode { get; set; }

    [JsonPropertyName("name")]
    public Dictionary<string, string>? Name { get; set; }
}

public class Keyword
{
    public required string Word { get; set; }
    public required string Language { get; set; }
}

internal class AccessListMembershipResponse
{
    [JsonPropertyName("data")]
    public List<AccessListMembership>? Data { get; set; }
}

internal class AccessListMembership
{
    [JsonPropertyName("party")]
    public string? Party { get; set; }

    [JsonPropertyName("resource")]
    public string? Resource { get; set; }

    [JsonPropertyName("since")]
    public DateTimeOffset? Since { get; set; }
}
