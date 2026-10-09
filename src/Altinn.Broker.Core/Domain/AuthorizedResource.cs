namespace Altinn.Broker.Core.Domain;

/// <summary>
/// The send/receive access one party has to a broker resource, as decided by Altinn Authorization (PDP).
/// Configuration (publish) access is evaluated separately via <c>CheckAccessAsPublisher</c> on the gatekeeper resource.
/// </summary>
public sealed record AuthorizedResource
{
    public required string ResourceId { get; init; }
    public bool CanSend { get; init; }
    public bool CanReceive { get; init; }
}
