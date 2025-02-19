namespace Altinn.Broker.Models;


/// <summary>
/// A model representing the initialization and upload of a file transfer.
/// </summary>
public class FileTransferInitializeAndUploadExt
{
    /// <summary>
    /// The metadata for the file transfer.
    /// </summary>
    public required FileTransferInitalizeExt Metadata { get; set; }

    /// <summary>
    /// The file to be uploaded.
    /// </summary>
    public required IFormFile FileTransfer { get; set; }
}
