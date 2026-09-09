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
}
