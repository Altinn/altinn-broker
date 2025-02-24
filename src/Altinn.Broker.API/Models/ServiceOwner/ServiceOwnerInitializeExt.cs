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
    /// How long the service owner should keep files before they are deleted. <br/>
    /// Must be in ISO8601 Duration
    /// </summary>
    public required string DeletionTime { get; set; }
}
