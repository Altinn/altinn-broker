namespace Altinn.Broker.Core.Services.Enums;

public enum AltinnEventType
{
    FileTransferInitialized,
    UploadProcessing,
    Published,
    UploadFailed,
    DownloadConfirmed,
    AllConfirmedDownloaded,
    FileDeleted,
    FileNeverConfirmedDownloaded
}
