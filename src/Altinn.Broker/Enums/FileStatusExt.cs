namespace Altinn.Broker.Enums
{
    public enum FileStatusExt
    {
        Initialized,
        UploadInProgress,
        AwaitingUploadProcessing,
        UploadedAndProcessed,
        Published,
        Cancelled,
        AllConfirmedDownloaded,
        Deleted,
        Failed
    }
}