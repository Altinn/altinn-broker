namespace Altinn.Broker.API.Models;

/// <summary>
/// The TUS partial uploads of a file transfer.
/// </summary>
public class PartialUploadsExt
{
    /// <summary>
    /// The partial uploads that still exist, in the order they were created.
    /// </summary>
    public List<PartialUploadExt> PartialUploads { get; set; } = new List<PartialUploadExt>();
}

/// <summary>
/// A TUS partial upload of a file transfer.
/// </summary>
public class PartialUploadExt
{
    /// <summary>
    /// The last segment of the partial upload's URL, /broker/api/v1/filetransfer/upload/tus/{fileTransferId}/partial/{partialUploadId}.
    /// </summary>
    public required string PartialUploadId { get; set; }

    /// <summary>
    /// The size of the partial upload in bytes, as given in its Upload-Length.
    /// </summary>
    public long UploadLength { get; set; }
}
