using System.Text.Json.Serialization;

namespace Altinn.Broker.Models;

/// <summary>
/// A broker resource (formidlingstjeneste) the authenticated end user has access to on behalf of a party.
/// </summary>
public class AuthorizedResourceExt
{
    /// <summary>
    /// The resource id in the Altinn Resource Registry
    /// </summary>
    [JsonPropertyName("resourceId")]
    public required string ResourceId { get; set; }

    /// <summary>
    /// The title of the resource. Null when the resource is not available in the Resource Registry
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The name of the service owner that owns the resource
    /// </summary>
    [JsonPropertyName("serviceOwnerName")]
    public string? ServiceOwnerName { get; set; }

    /// <summary>
    /// The party can initiate file transfers on the resource
    /// </summary>
    [JsonPropertyName("canSend")]
    public bool CanSend { get; set; }

    /// <summary>
    /// The party can find and download file transfers on the resource
    /// </summary>
    [JsonPropertyName("canReceive")]
    public bool CanReceive { get; set; }

    /// <summary>
    /// The party is a Broker service owner and the user has <c>publish</c> on
    /// <c>digdir-broker-administrasjon</c> for that party, so they may configure this resource
    /// (when it is owned by the party).
    /// </summary>
    [JsonPropertyName("canPublish")]
    public bool CanPublish { get; set; }

    /// <summary>
    /// Whether the requested party is configured as a Broker service owner.
    /// </summary>
    [JsonPropertyName("isServiceOwner")]
    public bool IsServiceOwner { get; set; }

    /// <summary>
    /// Whether the requested party owns this broker resource.
    /// </summary>
    [JsonPropertyName("isOwned")]
    public bool IsOwned { get; set; }

    /// <summary>
    /// The user has publish on <c>digdir-broker-administrasjon</c> for the requested party.
    /// </summary>
    [JsonPropertyName("hasConfigureAccess")]
    public bool HasConfigureAccess { get; set; }

    /// <summary>
    /// NotRegistered, InProgress, Ready, or Failed for the requested party.
    /// </summary>
    [JsonPropertyName("provisioningStatus")]
    public ServiceOwnerProvisioningStatus ProvisioningStatus { get; set; }

    /// <summary>
    /// The current deployment step. Null when the party is not registered.
    /// </summary>
    [JsonPropertyName("provisioningPhase")]
    public ProvisioningPhase? ProvisioningPhase { get; set; }

    /// <summary>
    /// Approximate provisioning completion, from 0 to 100.
    /// </summary>
    [JsonPropertyName("provisioningPercent")]
    public int ProvisioningPercent { get; set; }
}
