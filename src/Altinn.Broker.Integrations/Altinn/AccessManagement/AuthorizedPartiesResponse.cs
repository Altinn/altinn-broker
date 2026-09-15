using System.Text.Json.Serialization;

namespace Altinn.Broker.Integrations.Altinn.AccessManagement;

/// <summary>
/// One page of GET accessmanagement/api/v1/enduser/authorizedparties.
/// </summary>
internal sealed class AuthorizedPartiesResponse
{
    [JsonPropertyName("data")]
    public List<AuthorizedPartyDto> Data { get; set; } = [];

    [JsonPropertyName("links")]
    public AuthorizedPartiesLinks? Links { get; set; }
}

internal sealed class AuthorizedPartiesLinks
{
    /// <summary>Absolute url of the next page, or null when this was the last page.</summary>
    [JsonPropertyName("next")]
    public string? Next { get; set; }
}

internal sealed class AuthorizedPartyDto
{
    [JsonPropertyName("partyUuid")]
    public string? PartyUuid { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("organizationNumber")]
    public string? OrganizationNumber { get; set; }

    [JsonPropertyName("partyId")]
    public int PartyId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("onlyHierarchyElementWithNoAccess")]
    public bool OnlyHierarchyElementWithNoAccess { get; set; }

    [JsonPropertyName("subunits")]
    public List<AuthorizedPartyDto>? Subunits { get; set; }
}
