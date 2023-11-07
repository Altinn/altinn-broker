using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class FileEntity
{
    public Guid FileId { get; set; }
    public string ApplicationId { get; set; }
    public string Sender { get; set; } // Joined in
    public string ExternalFileReference { get; set; }
    public FileStatus FileStatus { get; set; }
    public DateTimeOffset FileStatusChanged { get; set; }
    public DateTimeOffset Uploaded { get; set; }
    public string FileLocation { get; set; } // Joined in
    public List<ActorFileStatusEntity> ActorEvents { get; set; } // Joined in
    public string Filename { get; set; }
    public string? Checksum { get; set; }
    public Dictionary<string, string> PropertyList { get; set; }
}