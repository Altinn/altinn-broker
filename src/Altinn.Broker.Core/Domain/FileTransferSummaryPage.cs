namespace Altinn.Broker.Core.Domain;

/// <summary>
/// One page of file transfer summaries, and how to continue past it.
/// </summary>
public class FileTransferSummaryPage
{
    public required List<FileTransferSummaryEntity> Summaries { get; set; }

    /// <summary>Pass back as the cursor to read the next page. Null when this is the last one.</summary>
    public string? ContinuationToken { get; set; }

    /// <summary>
    /// Whether the party has file transfers beyond this page. Derived rather than stored, so it
    /// cannot disagree with the token.
    /// </summary>
    public bool HasNextPage => ContinuationToken is not null;

    public static FileTransferSummaryPage Empty() => new() { Summaries = [] };
}
