namespace Altinn.Broker.Application.PurgeFileTransfer;

public enum PurgeTrigger
{
    FileTransferExpiry,
    AllConfirmedDownloaded,
    MalwareScanFailed
}

public class PurgeFileTransferRequest
{
    public Guid FileTransferId { get; set; }
    public PurgeTrigger PurgeTrigger { get; set; }
}
