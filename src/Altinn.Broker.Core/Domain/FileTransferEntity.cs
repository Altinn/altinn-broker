using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class FileTransferEntity
{
    public Guid FileTransferId { get; set; }
    public string ResourceId { get; set; }
    public ActorEntity Sender { get; set; } // Joined in
    public string SendersFileTransferReference { get; set; }
    public FileTransferStatusEntity FileTransferStatusEntity { get; set; } // Joined in
    public DateTimeOffset FileTransferStatusChanged { get; set; }
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset ExpirationTime { get; set; }
    public List<ActorFileTransferStatusEntity> RecipientCurrentStatuses { get; set; } // Joined in
    public string? FileLocation { get; set; }
    public string FileName { get; set; }
    public long FileTransferSize { get; set; }
    public string? Checksum { get; set; }
    public Dictionary<string, string> PropertyList { get; set; }
}
