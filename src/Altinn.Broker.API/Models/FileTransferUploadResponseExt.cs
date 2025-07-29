namespace Altinn.Broker.API.Models;

/// <summary>
/// Represents the response from uploading a file transfer.
/// </summary>
public class FileTransferUploadResponseExt
{
    /// <summary>
    /// The ID of the file transfer.
    /// </summary>
    public Guid FileTransferId { get; set; }
}
