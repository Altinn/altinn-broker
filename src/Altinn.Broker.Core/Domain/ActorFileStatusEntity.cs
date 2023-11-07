using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class ActorFileStatusEntity
{
    public Guid FileId { get; set; }
    public ActorEntity Actor { get; set; }
    public Enums.ActorFileStatus Status { get; set; }
    public DateTimeOffset Date { get; set; }
}
