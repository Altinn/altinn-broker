namespace Altinn.Broker.Application.GetAuthorizedResources;

public class AuthorizedResourceOverview
{
    public required string ResourceId { get; set; }
    public string? Name { get; set; }
    public string? ServiceOwnerName { get; set; }
    public bool CanSend { get; set; }
    public bool CanReceive { get; set; }
    public bool CanPublish { get; set; }
    /// <summary>Whether the requested party is configured as a Broker service owner.</summary>
    public bool IsServiceOwner { get; set; }
    /// <summary>Whether the requested party owns this broker resource.</summary>
    public bool IsOwned { get; set; }
    /// <summary>The user has publish on digdir-broker-administrasjon for the requested party.</summary>
    public bool HasConfigureAccess { get; set; }
    /// <summary>Whether the requested party's Broker storage is registered and deployed.</summary>
    public ServiceOwnerProvisioningStatus ProvisioningStatus { get; set; }
    /// <summary>Current deployment step. Null when the party is not registered.</summary>
    public ProvisioningPhase? ProvisioningPhase { get; set; }
    /// <summary>Approximate provisioning completion, from 0 to 100.</summary>
    public int ProvisioningPercent { get; set; }
}
