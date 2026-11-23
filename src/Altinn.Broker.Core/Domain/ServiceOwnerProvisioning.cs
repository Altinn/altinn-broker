namespace Altinn.Broker.Core.Domain;

/// <summary>
/// Maps the stored provisioning phase to the status and progress BrokerBox shows.
/// </summary>
public static class ServiceOwnerProvisioning
{
    /// <summary>
    /// A legacy row is <see cref="ProvisioningPhase.Ready"/> even before this column existed.
    /// A deployment that finished inserting storage but did not record <see cref="ProvisioningPhase.Ready"/>
    /// is treated as ready as well, so configuration is not blocked after the accounts exist.
    /// </summary>
    public static ProvisioningPhase EffectivePhase(ServiceOwnerEntity owner, bool development)
    {
        if (owner.ProvisioningPhase != ProvisioningPhase.Ready && HasRequiredStorage(owner, development))
        {
            return ProvisioningPhase.Ready;
        }

        return owner.ProvisioningPhase;
    }

    public static ServiceOwnerProvisioningStatus StatusFor(ProvisioningPhase phase) => phase switch
    {
        ProvisioningPhase.Ready => ServiceOwnerProvisioningStatus.Ready,
        ProvisioningPhase.Failed => ServiceOwnerProvisioningStatus.Failed,
        _ => ServiceOwnerProvisioningStatus.InProgress
    };

    public static int PercentFor(ProvisioningPhase? phase) => phase switch
    {
        ProvisioningPhase.Queued => 10,
        ProvisioningPhase.CreatingResourceGroup => 25,
        ProvisioningPhase.CreatingVirusScanStorage => 45,
        ProvisioningPhase.EnablingMalwareProtection => 65,
        ProvisioningPhase.CreatingStorage => 85,
        ProvisioningPhase.Ready => 100,
        _ => 0
    };

    /// <summary>
    /// Development only creates the virus-scan account. Other environments create both.
    /// </summary>
    public static bool HasRequiredStorage(ServiceOwnerEntity owner, bool development)
    {
        var hasVirusScanStorage = owner.GetStorageProvider(withVirusScan: true) is not null;
        if (development)
        {
            return hasVirusScanStorage;
        }

        return hasVirusScanStorage && owner.GetStorageProvider(withVirusScan: false) is not null;
    }
}
