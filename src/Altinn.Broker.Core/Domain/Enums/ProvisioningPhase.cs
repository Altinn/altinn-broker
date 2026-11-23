/// <summary>
/// How far service-owner provisioning has got. Storage deployment runs in the background and
/// moves through these phases over a minute or two.
/// </summary>
public enum ProvisioningPhase
{
    Queued,
    CreatingResourceGroup,
    CreatingVirusScanStorage,
    EnablingMalwareProtection,
    CreatingStorage,
    Ready,
    Failed
}

/// <summary>
/// Coarse provisioning state for BrokerBox. <see cref="NotRegistered"/> is not stored; it means
/// the organization has no service-owner row yet.
/// </summary>
public enum ServiceOwnerProvisioningStatus
{
    NotRegistered,
    InProgress,
    Ready,
    Failed
}
