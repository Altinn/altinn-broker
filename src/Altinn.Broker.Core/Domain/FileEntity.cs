using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class FileEntity
{
    public Guid FileId { get; set; }
    public string ResourceId { get; set; }
    public ActorEntity Sender { get; set; } // Joined in
    public string SendersFileReference { get; set; }
    public FileStatus FileStatus { get; set; }
    public DateTimeOffset FileStatusChanged { get; set; }
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset ExpirationTime { get; set; }
    public List<ActorFileStatusEntity> RecipientCurrentStatuses { get; set; } // Joined in
    public StorageProviderEntity StorageProvider { get; set; }
    public string? FileLocation { get; set; }
    public string Filename { get; set; }
    public string? Checksum { get; set; }
    public Dictionary<string, string> PropertyList { get; set; }
}
