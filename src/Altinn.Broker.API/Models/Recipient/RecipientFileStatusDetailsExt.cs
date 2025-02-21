using Altinn.Broker.Enums;

namespace Altinn.Broker.Models;

/// <summary>
/// Represents the current status of a file transfer for a specific recipient.
/// </summary>
public class RecipientFileTransferStatusDetailsExt
{
    /// <summary>
    /// The recipient of the file transfer.
    /// </summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// The current status code of the file transfer.
    /// </summary>
    public RecipientFileTransferStatusExt CurrentRecipientFileTransferStatusCode { get; set; }

    /// <summary>
    /// The current status text of the file transfer.
    /// </summary>
    public string CurrentRecipientFileTransferStatusText { get; set; } = string.Empty;

    /// <summary>
    /// The date and time when the status of the file transfer was last changed.
    /// </summary>
    public DateTimeOffset CurrentRecipientFileTransferStatusChanged { get; set; }
}
