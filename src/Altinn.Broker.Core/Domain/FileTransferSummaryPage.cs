namespace Altinn.Broker.Core.Domain;

/// <summary>
/// One page of file transfer summaries, and whether the party has more than fit in it.
/// </summary>
/// <remarks>
/// The list endpoints cap how many file transfers they return. Without <see cref="HasMore"/> a
/// capped list is indistinguishable from a complete one, which makes a search that finds nothing
/// look like the file transfer does not exist rather than like it was never loaded.
/// </remarks>
public class FileTransferSummaryPage
{
    public required List<FileTransferSummaryEntity> Summaries { get; set; }

    /// <summary>True when the party has file transfers beyond the ones in <see cref="Summaries"/>.</summary>
    public required bool HasMore { get; set; }

    public static FileTransferSummaryPage Empty() => new() { Summaries = [], HasMore = false };
}
