namespace Altinn.Broker.Core.Domain.Enums;

/// <summary>
/// Defines the type of recipients
/// </summary>
public enum RecipientType : int
{
    /// <summary>
    /// Specifies that the recipient is a person
    /// </summary>
    Person = 0,

    /// <summary>
    /// Specifies that the recipient is an organization
    /// </summary>
    Organization = 1,

    /// <summary>
    /// Specifies that the recipient type is unknown or could not be determined
    /// </summary>
    Unknown = 2,
}

