namespace Altinn.Broker.Core.Domain;

/// <summary>
/// The access one party has to a broker resource, as decided by Altinn Authorization (PDP).
/// </summary>
public sealed record AuthorizedResource
{
    public required string ResourceId { get; init; }

    /// <summary>
    /// Write access. The party can initiate file transfers on the resource.
    /// </summary>
    public bool CanSend { get; init; }

    /// <summary>
    /// Read access. The party can find and download file transfers on the resource.
    /// </summary>
    public bool CanReceive { get; init; }
}
