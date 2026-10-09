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

    [JsonPropertyName("nationalIdentityNumber")]
    public string? NationalIdentityNumber { get; set; }

    /// <summary>
    /// The organization number of the file transfer recipient this custom recipient should be notified on behalf of.
    /// Required, and must be one of the file transfer's recipients.
    /// </summary>
    [JsonPropertyName("relatedOrganizationNumber")]
    public string? RelatedOrganizationNumber { get; set; }
}
