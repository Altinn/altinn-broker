using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Application.DownloadFile;
public class DownloadFileResponse
{
    public required string FileName { get; set; }
    public required Stream DownloadStream { get; set; }
    /// <summary>
    /// Total size of the stored file, regardless of any requested range. Used for the Content-Range header.
    /// </summary>
    public long TotalSize { get; set; }
    /// <summary>
    /// The range the download was restricted to, resolved against the file size. Null when the full file is returned.
    /// </summary>
    public ByteRange? ResolvedRange { get; set; }
}
