using Altinn.Broker.Enums;

namespace Altinn.Broker.Models;

/// <summary>
/// Represents the status of a file transfer to a recipient.
/// </summary>
public class RecipientFileTransferStatusEventExt
{
    /// <summary>
    /// The recipient of the file transfer.
    /// </summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// The status code of the file transfer.
    /// </summary>
    public RecipientFileTransferStatusExt RecipientFileTransferStatusCode { get; set; }

    /// <summary>
    /// The status text of the file transfer.   
    /// </summary>
    public string RecipientFileTransferStatusText { get; set; } = string.Empty;

    /// <summary>
    /// The date and time when the status of the file transfer was last changed.
    /// </summary>
    public DateTimeOffset RecipientFileTransferStatusChanged { get; set; }
}
