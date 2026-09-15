namespace Altinn.Broker.Core.Domain;

/// <summary>
/// A party the authenticated end user is authorized to represent, as reported by Altinn Access Management.
/// </summary>
public sealed record AuthorizedParty
{
    public required string PartyUuid { get; init; }
    public string? Name { get; init; }

    /// <summary>Null for parties that are not organizations.</summary>
    public string? OrganizationNumber { get; init; }
    public int PartyId { get; init; }

    /// <summary>Party type as reported by Access Management: Person, Organization, SelfIdentified or None.</summary>
    public required string Type { get; init; }
    public bool IsDeleted { get; init; }

    /// <summary>The party is only present to carry its subunits. The user has no access to the party itself.</summary>
    public bool OnlyHierarchyElementWithNoAccess { get; init; }
    public IReadOnlyList<AuthorizedParty> Subunits { get; init; } = [];
}
