using System.Text.Json.Serialization;

namespace Altinn.Broker.Models;

/// <summary>
/// A party the authenticated end user can act on behalf of.
/// </summary>
public class AuthorizedPartyExt
{
    /// <summary>
    /// The party uuid in Altinn Register
    /// </summary>
    [JsonPropertyName("partyUuid")]
    public required string PartyUuid { get; set; }

    /// <summary>
    /// Display name of the party
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The organization number. Null for parties that are not organizations
    /// </summary>
    [JsonPropertyName("organizationNumber")]
    public string? OrganizationNumber { get; set; }

    /// <summary>
    /// The party id in Altinn Register
    /// </summary>
    [JsonPropertyName("partyId")]
    public int PartyId { get; set; }

    /// <summary>
    /// Party type: Person, Organization, SelfIdentified or None
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// The party is deleted in Altinn Register
    /// </summary>
    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    /// <summary>
    /// The party is only present to carry its subunits. The user has no access to the party itself
    /// </summary>
    [JsonPropertyName("onlyHierarchyElementWithNoAccess")]
    public bool OnlyHierarchyElementWithNoAccess { get; set; }

    /// <summary>
    /// Subunits of the party that the user can also act on behalf of
    /// </summary>
    [JsonPropertyName("subunits")]
    public List<AuthorizedPartyExt> Subunits { get; set; } = [];
}
