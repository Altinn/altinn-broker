namespace Altinn.Broker.Core.Domain;

public class ActorFileTransferStatusEntity
{
    public Guid FileTransferId { get; set; }
    public required ActorEntity Actor { get; set; }
    public Enums.ActorFileTransferStatus Status { get; set; }
    public DateTimeOffset Date { get; set; }
}
