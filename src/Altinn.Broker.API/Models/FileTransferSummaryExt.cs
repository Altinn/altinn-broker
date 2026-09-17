namespace Altinn.Broker.Models;

/// <summary>
/// Lean summary of a file transfer, for list views (active or historical)
/// </summary>
public class FileTransferSummaryExt
{
    public Guid FileTransferId { get; set; }

    /// <summary>
    /// The Altinn resource ID for the broker service
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Sender of the file transfer
    /// </summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>
    /// Recipients of the file transfer
    /// </summary>
    public List<string> Recipients { get; set; } = new List<string>();

    /// <summary>
    /// Used by senders and receivers to identify specific file using external identification methods.
    /// </summary>
    public string SendersFileTransferReference { get; set; } = string.Empty;
}
