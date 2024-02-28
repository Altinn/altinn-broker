namespace Altinn.Broker.Core.Domain;
public class FileTransferStatusEntity
{
    public Guid FileTransferId { get; set; }
    public Enums.FileTransferStatus Status { get; set; }
    public DateTimeOffset Date { get; set; }
    public string? DetailedStatus { get; set; }
}
