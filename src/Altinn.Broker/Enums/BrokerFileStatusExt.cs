namespace Altinn.Broker.Enums
{
    public enum BrokerFileStatusExt
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
