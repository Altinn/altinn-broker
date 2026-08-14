namespace Altinn.Broker.Application.UploadFile;

public class CompleteFileUploadRequest
{
    public Guid FileTransferId { get; set; }
    public string? Checksum { get; set; }
    public long UploadLength { get; set; }
    public long? StripeSizeBytes { get; set; }
    public bool DeferChecksumValidation { get; set; }
    public DateTimeOffset UploadFinishedTimestamp { get; set; }
}
