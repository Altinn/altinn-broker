using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class File
{
    public Guid FileId { get; set; }
    public string Sender { get; set; } // Joined in
    public string ExternalFileReference { get; set; }
    public FileStatus FileStatus { get; set; }
    public DateTimeOffset? LastStatusUpdate { get; set; }
    public DateTimeOffset Uploaded { get; set; }
    public string FileLocation { get; set; } // Joined in
    public List<ActorFileStatus> ActorEvents { get; set; } // Joined in
    public string Filename { get; set; }
    public string Checksum { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public DateTime FileStatusChanged { get; set; }
}
