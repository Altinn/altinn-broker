namespace Altinn.Broker.API.Models;

/// <summary>
/// Represents the response from initializing a file transfer.
/// </summary>
public class FileTransferInitializeResponseExt
{
    /// <summary>
    /// The ID of the file transfer.
    /// </summary>
    public Guid FileTransferId { get; set; }
}
