namespace Altinn.Broker.Core.Domain;

/// <summary>
/// The parties on a resource's access lists, and whether the resource restricts recipients at all.
/// </summary>
public sealed record ResourceAccessList
{
    /// <summary>
    /// False when the resource has no access list.
    /// </summary>
    public required bool Restricted { get; init; }

    /// <summary>Party uuids of the members. Always empty when <see cref="Restricted"/> is false.</summary>
    public required List<string> PartyUuids { get; init; }
}
