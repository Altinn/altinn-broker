namespace Altinn.Broker.Core.Domain;

/// <summary>
/// One page of file transfer summaries, and how to continue past it.
/// </summary>
public class FileTransferSummaryPage
{
    public required List<FileTransferSummaryEntity> Summaries { get; set; }

    /// <summary>True when the party has file transfers beyond the ones in <see cref="Summaries"/>.</summary>
    public required bool HasNextPage { get; set; }

    /// <summary>Pass back as the cursor to read the next page. Null when there is none.</summary>
    public string? ContinuationToken { get; set; }

    public static FileTransferSummaryPage Empty() => new() { Summaries = [], HasNextPage = false };
}
