using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class FileReceipt {
    public Guid FileId { get; set; }
    public Actor Actor { get; set; }
    public ActorFileStatus Status { get; set; }
    public DateTimeOffset Date { get; set; }
}
