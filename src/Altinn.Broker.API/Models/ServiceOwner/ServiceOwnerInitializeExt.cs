namespace Altinn.Broker.Models.ServiceOwner;

/// <summary>
/// Represents the Broker properties of a service owner.
/// </summary>
public class ServiceOwnerInitializeExt
{
    /// <summary>
    /// The name of the service owner.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The organization id of the service owner to be setup. This parameter can only be used by Digdir.
    /// </summary>
    public string? OrganizationId { get; set; }
}
