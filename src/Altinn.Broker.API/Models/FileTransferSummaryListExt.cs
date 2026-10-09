namespace Altinn.Broker.Models;

/// <summary>
/// One page of file transfer summaries, newest first, and how to continue past it.
/// </summary>
public class FileTransferSummaryListExt
{
    /// <summary>
    /// The file transfers on this page.
    /// </summary>
    public List<FileTransferSummaryExt> Items { get; set; } = new List<FileTransferSummaryExt>();

    /// <summary>
    /// True when the party has file transfers beyond the ones in <see cref="Items"/>.
    /// </summary>
    public bool HasNextPage { get; set; }

    /// <summary>
    /// Pass back as <c>continuationToken</c> to read the next page. Null when there is none.
    /// </summary>
    public string? ContinuationToken { get; set; }
}
