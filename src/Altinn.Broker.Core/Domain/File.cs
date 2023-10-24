using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Domain;

public class File
{
    public Guid FileId { get; set; }
    public string ExternalFileReference { get; set; };
    public Guid ShipmentId { get; set; }
    public FileStatus FileStatus { get; set; } // Joined in
    public DateTimeOffset? LastStatusUpdate { get; set; }
    public DateTimeOffset Uploaded { get; set; }
    public string FileLocation { get; set; }; // Joined in
    public List<FileReceipt> Receipts { get; set; } // Joined in
}
