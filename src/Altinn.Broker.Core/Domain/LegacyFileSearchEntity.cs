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

    public bool TryGetActorIds(out long[]? actorIdArray)
    {
        List<long> actorIds = new List<long>();
        actorIdArray = null;
        if (Actor is not null)
        {
            actorIds.Add(Actor.ActorId);
        }

        if (Actors is not null)
        {
            actorIds.AddRange(Actors.Select(a => a.ActorId));
        }

        if (actorIds.Count > 0)
        {
            actorIdArray = actorIds.Distinct().ToArray();
            return true;
        }

        return false;
    }
}
