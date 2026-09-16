using System.Text.Json.Serialization;

namespace Altinn.Broker.Models;

/// <summary>
/// An organization the sending party may address a file transfer to on a resource.
/// </summary>
public class AllowedRecipientExt
{
    /// <summary>
    /// The organization number of the recipient
    /// </summary>
    [JsonPropertyName("organizationNumber")]
    public required string OrganizationNumber { get; set; }

    /// <summary>
    /// The name of the organization. Null when Altinn Register had no name for it
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
