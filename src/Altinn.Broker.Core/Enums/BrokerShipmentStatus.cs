namespace Altinn.Broker.Core.Enums
{
    public enum BrokerShipmentStatus
    {
        Initialized,
        UploadInProgress,
        AwaitingUploadProcessing,
        AllFilesUploadedAndProcessed,
        Published,
        Cancelled,
        Completed,
        Deleted,
        Failed
    }
}
