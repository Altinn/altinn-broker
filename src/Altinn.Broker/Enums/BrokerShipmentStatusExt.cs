namespace Altinn.Broker.Enums
{
    public enum BrokerShipmentStatusExt
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