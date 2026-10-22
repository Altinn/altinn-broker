
namespace Altinn.Broker.Core.Models.Notifications;

public class Recipient
{
    public string? EmailAddress { get; set; }

    public string? MobileNumber { get; set; }

    /// <summary>
    /// The file transfer's own recipients. Custom recipients identified by organization number are disabled for now.
    /// </summary>
    public string? OrganizationNumber { get; set; }

    /// <summary>
    /// For custom recipients only: the file transfer recipient (organization number) this notification is about.
    /// </summary>
    public string? RelatedOrganizationNumber { get; set; }
}
