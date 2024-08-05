namespace Altinn.Broker.Application.ExpireFileTransfer;

public class ExpireFileTransferRequest
{
    public Guid FileTransferId { get; set; }
    public bool Force { get; set; }

    /// <summary>
    /// When this is set, the ExpireFileTransferHandler will delete the file from storage, but will refrain from setting the FileTransferStatus to Purged in FileTransferRepository.
    /// This is used when MalwareScanResultHandler fails an uploaded file.
    /// </summary>
    public bool DoNotUpdateStatus { get; set; }
}
