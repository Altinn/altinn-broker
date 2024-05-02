namespace Altinn.Broker.Core.Services.Enums;

public enum AltinnEventType
{
    FileTransferInitialized,
    UploadProcessing,
    Published,
    UploadFailed,
    DownloadConfirmed,
    AllConfirmedDownloaded,
    FilePurged,
    FileNeverConfirmedDownloaded
}
