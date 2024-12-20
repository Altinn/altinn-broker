﻿namespace Altinn.Broker.Models.ServiceOwner;

public class ServiceOwnerOverviewExt
{
    public ServiceOwnerOverviewExt() { }

    public required string Name { get; set; }

    public required List<StorageProviderExt> StorageProviders { get; set; } = new List<StorageProviderExt>();
}

public class StorageProviderExt
{
    public required StorageProviderTypeExt Type { get; set; }
    public required DeploymentStatusExt DeploymentStatus { get; set; }
    public required string DeploymentEnvironment { get; set; }
}

public enum StorageProviderTypeExt
{
    AltinnAzure = 0,
    AltinnAzureWithoutVirusScan = 1,
}
