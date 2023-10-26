namespace Altinn.Broker.Core.Enums
{
    public enum BrokerFileStatus
    {
        Initialized,
        AwaitingUpload,
        UploadInProgress,
        AwaitingUploadProcessing,
        UploadedAndProcessed,
        Published,
        Cancelled,
        Downloaded,
        AllConfirmedDownloaded,
        Deleted,
        Failed
    }
}