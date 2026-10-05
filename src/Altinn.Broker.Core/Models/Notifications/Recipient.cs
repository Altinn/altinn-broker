
namespace Altinn.Broker.Core.Models.Notifications;

public class Recipient
{
    public string? EmailAddress { get; set; }

    public string? MobileNumber { get; set; }

    public string? OrganizationNumber { get; set; }

    public string? NationalIdentityNumber { get; set; }

    /// <summary>
    /// For custom recipients only: the file transfer recipient (organization number) this notification is about.
    /// </summary>
    public string? RelatedOrganizationNumber { get; set; }
}
