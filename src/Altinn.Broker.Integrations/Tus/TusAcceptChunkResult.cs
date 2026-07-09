namespace Altinn.Broker.Integrations.Tus;

public enum TusAcceptChunkStatus
{
    Accepted,
    NotFound,
    Conflict,
    Overflow
}

public sealed record TusAcceptChunkResult(
    TusAcceptChunkStatus Status,
    long CurrentAcceptedOffset,
    long NewAcceptedOffset,
    long BlockIndex);
