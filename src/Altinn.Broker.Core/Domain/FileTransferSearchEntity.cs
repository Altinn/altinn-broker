using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class FileTransferSearchEntity
{
    public ActorEntity Actor { get; set; }
    public FileTransferStatus? Status { get; set; }
    public ActorFileTransferStatus? RecipientStatus { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string ResourceId { get; set; }
}
