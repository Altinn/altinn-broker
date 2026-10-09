namespace Altinn.Broker.Core.Models.Enums;

/// <summary>
/// Which identifier a custom notification recipient is identified by.
/// </summary>
public enum CustomRecipientType
{
    Organization = 0,
    Person = 1,
    Email = 2,
    MobileNumber = 3,
}
