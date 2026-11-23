using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.ProvisionServiceOwner;

public class ServiceOwnerProvisioningState
{
    public required ServiceOwnerProvisioningStatus Status { get; init; }
    public ProvisioningPhase? Phase { get; init; }
    public int Percent { get; init; }

    public static ServiceOwnerProvisioningState NotRegistered { get; } = new()
    {
        Status = ServiceOwnerProvisioningStatus.NotRegistered,
        Phase = null,
        Percent = 0
    };

    public static ServiceOwnerProvisioningState FromPhase(ProvisioningPhase phase) => new()
    {
        Status = ServiceOwnerProvisioning.StatusFor(phase),
        Phase = phase,
        Percent = ServiceOwnerProvisioning.PercentFor(phase)
    };
}
