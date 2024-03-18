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
}
