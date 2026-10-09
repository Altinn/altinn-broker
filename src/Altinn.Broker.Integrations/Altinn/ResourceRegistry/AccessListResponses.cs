using System.Text.Json.Serialization;

namespace Altinn.Broker.Integrations.Altinn.ResourceRegistry;

/// <summary>
/// Responses from the paginated access list endpoints in Altinn Resource Registry.
/// </summary>
internal class PaginatedResponse<T>
{
    [JsonPropertyName("links")]
    public PaginatedLinks? Links { get; set; }

    [JsonPropertyName("data")]
    public List<T>? Items { get; set; }
}

internal class PaginatedLinks
{
    [JsonPropertyName("next")]
    public string? Next { get; set; }
}

internal class AccessListInfoResponse
{
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    /// <summary>
    /// Only populated when the request asks for <c>include=resources</c>, and then filtered to the
    /// requested resource. Empty means the list is not connected to it.
    /// </summary>
    [JsonPropertyName("resourceConnections")]
    public List<AccessListResourceConnectionResponse>? ResourceConnections { get; set; }
}

internal class AccessListResourceConnectionResponse
{
    [JsonPropertyName("resourceIdentifier")]
    public string? ResourceIdentifier { get; set; }
}

internal class AccessListMemberResponse
{
    /// <summary>The party as a URN, on the form <c>urn:altinn:party:uuid:{guid}</c>.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}
