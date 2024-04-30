namespace Altinn.Broker.Application.ExpireFileTransferCommand;

public class ExpireFileTransferCommandRequest
{
    public Guid FileTransferId { get; set; }
    public bool Force { get; set; }

    /// <summary>
    /// When this is set, the ExpireFileTransferCommandHandler will delete the file from storage, but will refrain from setting the FileTransferStatus to Deleted in FileTransferRepository.
    /// This is used when MalwareScanResultHandler fails an uploaded file.
    /// </summary>
    public bool DoNotUpdateStatus  {get;set; }
}
