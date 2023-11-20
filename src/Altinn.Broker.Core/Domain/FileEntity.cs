using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class FileEntity
{
    public Guid FileId { get; set; }
    public string ServiceOwnerId { get; set; }
    public string Sender { get; set; } // Joined in
    public string SendersFileReference { get; set; }
    public FileStatus FileStatus { get; set; }
    public DateTimeOffset FileStatusChanged { get; set; }
    public DateTimeOffset Uploaded { get; set; }
    public List<ActorFileStatusEntity> ActorEvents { get; set; } // Joined in
    public StorageProviderEntity StorageProvider { get; set; }
    public string? FileLocation { get; set; }
    public string Filename { get; set; }
    public string? Checksum { get; set; }
    public Dictionary<string, string> PropertyList { get; set; }
}
