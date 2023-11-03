namespace Altinn.Broker.Core.Domain.Enums;

public enum FileStatus
{
    Initialized = 0,
    AwaitingUpload = 1,
    UploadInProgress = 2,
    AwaitingUploadProcessing = 3,
    UploadedAndProcessed = 4,
    Published = 5,
    Cancelled = 6,
    Downloaded = 7,
    AllConfirmedDownloaded = 8,
    Deleted = 9,
    Failed = 10
}