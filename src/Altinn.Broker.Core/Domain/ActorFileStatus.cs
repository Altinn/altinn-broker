using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class ActorFileStatus {
    public Guid FileId { get; set; }
    public Actor Actor { get; set; }
    public Enums.ActorFileStatus Status { get; set; }
    public DateTimeOffset Date { get; set; }
}
