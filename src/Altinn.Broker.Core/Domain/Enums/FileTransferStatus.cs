namespace Altinn.Broker.Core.Domain.Enums;

public enum FileTransferStatus
{
    Initialized = 0,
    UploadStarted = 1,
    UploadProcessing = 2,
    Published = 3,
    Cancelled = 4,
    AllConfirmedDownloaded = 5,
    Purged = 6,
    Failed = 7
}
