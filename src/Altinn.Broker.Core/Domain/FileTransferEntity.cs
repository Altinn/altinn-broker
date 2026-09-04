namespace Altinn.Broker.Core.Domain;

public class FileTransferEntity
{
    public required Guid FileTransferId { get; set; }
    public required string ResourceId { get; set; }
    public required ActorEntity Sender { get; set; } // Joined in
    public string? SendersFileTransferReference { get; set; }
    public required FileTransferStatusEntity FileTransferStatusEntity { get; set; } // Joined in
    public DateTimeOffset FileTransferStatusChanged { get; set; }
    public required DateTimeOffset Created { get; set; }
    public required DateTimeOffset ExpirationTime { get; set; }
    public required List<ActorFileTransferStatusEntity> RecipientCurrentStatuses { get; set; } // Joined in
    public string? FileLocation { get; set; }
    public string? HangfireJobId { get; set; }
    public required string FileName { get; set; }
    public long FileTransferSize { get; set; } = 0;
    /// <summary>
    /// Bytes per stripe blob, or null when the content is a single block blob named {FileTransferId}.
    /// See <see cref="StripeLayout"/>.
    /// </summary>
    public long? StripeSizeBytes { get; set; }
    public string? Checksum { get; set; }
    public bool UseVirusScan { get; set; }
    public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();
}
