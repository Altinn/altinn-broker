using System.Text.Json.Serialization;

namespace Altinn.Broker.Models;

/// <summary>
/// An additional notification recipient, beyond the file transfer's own recipients.
/// </summary>
public class NotificationRecipientExt
{
    [JsonPropertyName("emailAddress")]
    public string? EmailAddress { get; set; }

    [JsonPropertyName("mobileNumber")]
    public string? MobileNumber { get; set; }

    [JsonPropertyName("organizationNumber")]
    public string? OrganizationNumber { get; set; }

    [JsonPropertyName("externalIdentity")]
    public string? ExternalIdentity { get; set; }
}
