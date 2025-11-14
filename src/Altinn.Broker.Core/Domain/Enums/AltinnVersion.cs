namespace Altinn.Broker.Core.Domain.Enums;

/// <summary>
/// Specifies the Altinn version for a file transfer
/// </summary>
public enum AltinnVersion
{
    /// <summary>
    /// File transfer from Altinn 2 (legacy system)
    /// </summary>
    Altinn2 = 0,

    /// <summary>
    /// File transfer from Altinn 3 (current system)
    /// </summary>
    Altinn3 = 1
}

