namespace Altinn.Broker.Core.Domain;

/// <summary>
/// The access one party has to a broker resource, as decided by Altinn Authorization (PDP).
/// <see cref="CanSend"/> is write access, <see cref="CanReceive"/> is read access.
/// </summary>
public sealed record AuthorizedResource
{
    public required string ResourceId { get; init; }
    public bool CanSend { get; init; }
    public bool CanReceive { get; init; }
}
