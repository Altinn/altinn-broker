using Altinn.Broker.Enums;

namespace Altinn.Broker.Core.Models;

/// <summary>
/// Represents the status of a file transfer.
/// </summary>
public class FileTransferStatusEventExt
{
    /// <summary>
    /// The status code of the file transfer.
    /// </summary>
    public FileTransferStatusExt FileTransferStatus { get; set; }

    /// <summary>
    /// The status text of the file transfer.
    /// </summary>
    public string FileTransferStatusText { get; set; } = string.Empty;

    /// <summary>
    /// The date and time when the status of the file transfer was last changed.
    /// </summary>
    public DateTimeOffset FileTransferStatusChanged { get; set; }
}
