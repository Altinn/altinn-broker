using System.Text.Json.Serialization;

namespace Altinn.Broker.Integrations.Altinn.ResourceRegistry;

/// <summary>Subset of Resource Registry search result fields used by Broker.</summary>
internal class SearchResourceResponse
{
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    [JsonPropertyName("title")]
    public Dictionary<string, string>? Title { get; set; }

    [JsonPropertyName("hasCompetentAuthority")]
    public HasCompetentAuthority? HasCompetentAuthority { get; set; }

    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; set; }
}
