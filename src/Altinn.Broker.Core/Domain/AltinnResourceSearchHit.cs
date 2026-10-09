namespace Altinn.Broker.Core.Domain;

/// <summary>
/// A resource returned from Altinn Resource Registry search.
/// </summary>
public sealed class AltinnResourceSearchHit
{
    public required string Id { get; init; }
    /// <summary>Competent authority organization number, without URN/legacy prefix.</summary>
    public required string OrganizationNumber { get; init; }
    public string? Title { get; init; }
    public string? ServiceOwnerName { get; init; }
    public string? ResourceType { get; init; }
}
