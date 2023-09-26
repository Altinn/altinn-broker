namespace Altinn.Broker.Core.Enums
{
    public enum BrokerFileStatus
    {
        Initialized,
        Uploaded,
        VirusScanInProgress,
        VirusScanOK,
        Published,
        Downloaded,
        VirusScanError,
        UploadError,
        InternalError,
        Deleted
    }
}