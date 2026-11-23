using System.Text.Json.Serialization;

namespace Altinn.Broker.Models.ServiceOwner;

/// <summary>
/// Provisioning progress for the service owner the end user acts on behalf of.
/// </summary>
public class ServiceOwnerProvisioningExt
{
    /// <summary>
    /// NotRegistered, InProgress, Ready, or Failed.
    /// </summary>
    [JsonPropertyName("status")]
    public required ServiceOwnerProvisioningStatus Status { get; set; }

    /// <summary>
    /// The current deployment step. Null when the service owner is not registered.
    /// </summary>
    [JsonPropertyName("phase")]
    public ProvisioningPhase? Phase { get; set; }

    /// <summary>
    /// Approximate completion, from 0 to 100.
    /// </summary>
    [JsonPropertyName("percent")]
    public int Percent { get; set; }
}

/// <summary>
/// Starts provisioning for the organization in the <c>party</c> query parameter.
/// </summary>
public class ServiceOwnerProvisioningRequestExt
{
    /// <summary>
    /// Display name stored for the service owner. BrokerBox sends the organization name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}
