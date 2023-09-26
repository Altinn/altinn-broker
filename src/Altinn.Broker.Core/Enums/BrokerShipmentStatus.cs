namespace Altinn.Broker.Core.Enums
{
    public enum BrokerShipmentStatus
    {
        //
        Initialized,
        RequiresSenderInteraction,
        ReadyToPublish,
        Published,
        VirusScanError,
        UploadError,
        InternalError,
        Deleted
    }
}