using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class LegacyFileSearchEntity
{
    public ActorEntity? Actor { get; set; }
    public List<ActorEntity>? Actors { get; set; }
    public ActorFileStatus? RecipientStatus { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? ResourceId { get; set; }
}
