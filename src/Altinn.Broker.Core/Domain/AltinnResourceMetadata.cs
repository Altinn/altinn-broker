namespace Altinn.Broker.Core.Domain;

/// <summary>
/// Presentation metadata about a resource, as registered in the Altinn Resource Registry.
/// </summary>
public sealed record AltinnResourceMetadata
{
    public string? Title { get; init; }
    public string? ServiceOwnerName { get; init; }
}
