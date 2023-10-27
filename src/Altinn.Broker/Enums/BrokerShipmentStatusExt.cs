namespace Altinn.Broker.Enums
{
    public enum BrokerShipmentStatusExt
    {
        Initialized,
        UploadInProgress,
        AwaitingUploadProcessing,
        AllFilesUploadedAndProcessed,
        Published,
        Cancelled,
        Completed,
        Expired,
        Deleted,
        Failed
    }
}
