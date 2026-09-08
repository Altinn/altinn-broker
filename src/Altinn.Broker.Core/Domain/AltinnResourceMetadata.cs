namespace Altinn.Broker.Core.Domain;

/// <summary>
/// Presentation metadata about a resource, as registered in the Altinn Resource Registry.
/// </summary>
public sealed record AltinnResourceMetadata
{
    /// <summary>
    /// The title of the resource, preferring Norwegian bokmål.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The name of the service owner that owns the resource, e.g. "Digitaliseringsdirektoratet".
    /// </summary>
    public string? ServiceOwnerName { get; init; }
}
