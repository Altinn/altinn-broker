﻿namespace Altinn.Broker.Core.Domain;

public class ServiceOwnerEntity
{
    public string Id { get; set; }
    public string Name { get; set; }
    public StorageProviderEntity? StorageProvider { get; set; }
    public TimeSpan FileTransferTimeToLive { get; set; }
}
