namespace Altinn.Broker.Core.Domain;

/// <summary>
/// Lean projection of a file transfer for list views - only what's needed to render a summary row,
/// as opposed to <see cref="FileTransferEntity"/> which carries the full detail set.
/// </summary>
public class FileTransferSummaryEntity
{
    public required Guid FileTransferId { get; set; }

    public required string ResourceId { get; set; }

    public required string Sender { get; set; }

    public required List<string> Recipients { get; set; }

    public required string SendersFileTransferReference { get; set; }
}
