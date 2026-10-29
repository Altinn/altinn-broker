namespace Altinn.Broker.Core.Models.Enums;

/// <summary>
/// Which identifier a custom notification recipient is identified by.
/// </summary>
public enum CustomRecipientType
{
    Email = 0,
    MobileNumber = 1,

    // Custom recipient organization number - disabled for now.
    // Organization = 2,
}
