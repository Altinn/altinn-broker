namespace Altinn.Broker.Models;

/// <summary>
/// A capped list of file transfer summaries, and whether the party has more than fit in it.
/// </summary>
public class FileTransferSummaryListExt
{
    /// <summary>
    /// The file transfers, newest first. At most <see cref="PageSize"/> of them.
    /// </summary>
    public List<FileTransferSummaryExt> Items { get; set; } = new List<FileTransferSummaryExt>();

    /// <summary>
    /// True when the party has file transfers beyond the ones in <see cref="Items"/>. Narrow the
    /// search with <c>from</c>/<c>to</c> to reach them.
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// The cap applied to <see cref="Items"/>.
    /// </summary>
    public int PageSize { get; set; }
}
