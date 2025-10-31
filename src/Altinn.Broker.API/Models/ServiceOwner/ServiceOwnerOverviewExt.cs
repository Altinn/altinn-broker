namespace Altinn.Broker.Models.ServiceOwner;

/// <summary>
/// Represents the Broker properties of a service owner.
/// </summary>
public class ServiceOwnerOverviewExt
{
    public ServiceOwnerOverviewExt() { }

    /// <summary>
    /// The name of the service owner.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The service owner's storage providers.
    /// </summary>
    public required List<StorageProviderExt> StorageProviders { get; set; } = new List<StorageProviderExt>();
}


/// <summary>
/// Represents the Broker properties of a storage provider.
/// </summary>
public class StorageProviderExt
{
    /// <summary>
    /// The Storage provider type. 
    /// </summary>
    public required StorageProviderTypeExt Type { get; set; }

    /// <summary>
    /// The deployment status of the storage provider.
    /// </summary>
    public required DeploymentStatusExt DeploymentStatus { get; set; }

    /// <summary>
    /// The deployment environment of the storage provider.
    /// </summary>
    public required string DeploymentEnvironment { get; set; }
}

/// <summary>
/// Represents the storage provider type. 
/// </summary>
public enum StorageProviderTypeExt
{
    /// <summary>
    /// Azure storage provider which scans files for viruses.
    /// </summary>
    AltinnAzure = 0,

    /// <summary>
    /// Azure storage provider which does not scan files for viruses.
    /// </summary>
    AltinnAzureWithoutVirusScan = 1,
}
