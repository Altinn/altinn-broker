namespace Altinn.Broker.Core.Domain.Enums;

public enum FileStatus
{
    Initialized = 0,
    UploadInProgress = 1,
    AwaitingUploadProcessing = 2,
    Published = 3,
    Cancelled = 4,
    AllConfirmedDownloaded = 5,
    Deleted = 6,
    Failed = 7
}