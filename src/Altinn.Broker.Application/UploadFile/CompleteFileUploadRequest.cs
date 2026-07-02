namespace Altinn.Broker.Application.UploadFile;

public class CompleteFileUploadRequest
{
    public Guid FileTransferId { get; set; }
    public required string Checksum { get; set; }
    public long UploadLength { get; set; }
}
