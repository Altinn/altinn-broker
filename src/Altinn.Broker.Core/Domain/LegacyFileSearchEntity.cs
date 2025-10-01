using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class LegacyFileSearchEntity
{
    public ActorEntity? Actor { get; set; }
    public List<ActorEntity>? Actors { get; set; }
    public ActorFileTransferStatus? RecipientFileTransferStatus { get; set; }
    public FileTransferStatus? FileTransferStatus { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? ResourceId { get; set; }

    public long[] GetActorIds()
    {
        List<long> actorIds = new();
        if (Actor is not null)
        {
            actorIds.Add(Actor.ActorId);
        }

        if (Actors is not null)
        {
            actorIds.AddRange(Actors.Select(a => a.ActorId));
        }

        return [.. actorIds];
    }
}
