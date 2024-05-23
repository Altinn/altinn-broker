namespace Altinn.Broker.Enums;

public enum FileTransferStatusExt
{
    Initialized,
    UploadStarted,
    UploadProcessing,
    Published,
    Cancelled,
    AllConfirmedDownloaded,
    Purged,
    Failed
}
