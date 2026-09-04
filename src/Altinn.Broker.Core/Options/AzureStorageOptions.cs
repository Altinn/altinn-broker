using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Options;

/// <summary>
/// Configuration options for Azure Storage block blob operations.
/// Used primarily for controlling large file upload behavior.
/// </summary>
public class AzureStorageOptions
{
    /// <summary>
    /// Size of each block in bytes. Must be between 1MB and 4000MB.
    /// </summary>
    public int BlockSize { get; set; }

    /// <summary>
    /// Number of concurrent threads for parallel upload operations.
    /// </summary>
    public int ConcurrentUploadThreads { get; set; }

    /// <summary>
    /// Number of blocks to upload before committing to Azure Storage.
    /// </summary>
    public int BlocksBeforeCommit { get; set; }

    /// <summary>
    /// Bytes per stripe blob, and therefore the size above which a transfer spans several blobs.
    /// Frozen onto each transfer at initialize, so raising or lowering it never relayouts content that
    /// is already stored. It also fixes the minimum chunk size clients must send
    /// (<see cref="StripeSizeBytes"/> / <see cref="MaxBlocksPerStripe"/>).
    /// </summary>
    public long StripeSizeBytes { get; set; } = 274_877_906_944; // 256 GiB

    /// <summary>
    /// Blocks a single stripe blob may hold. Azure's hard limit is 50 000; it is configurable only so
    /// tests can shrink it and exercise the budget without moving large amounts of data.
    /// </summary>
    public int MaxBlocksPerStripe { get; set; } = 50_000;

    /// <summary>
    /// Partials one concatenation can list. The concat-final request carries every partial's path in a
    /// single Upload-Concat header, roughly 117 bytes each, sharing Kestrel's
    /// MaxRequestHeadersTotalSize budget with the bearer token. Raise this only together with that
    /// limit: a transfer needing more partials than the header can carry fails at the final request,
    /// after all of its content has already been uploaded.
    /// </summary>
    public int MaxConcatPartials { get; set; } = 250;

    /// <summary>
    /// The largest file transfer that can be stored, and therefore the ceiling for a resource's
    /// configured maximum transfer size. The lesser of what the stripe index can name and what one
    /// concatenation can list; the partial count binds first at the default configuration. Capping it
    /// here means an unreachable size is rejected when a resource is configured, rather than at the
    /// concat-final request once the content has already been transferred.
    /// </summary>
    public long MaxTotalTransferBytes => Math.Min(
        StripeSizeBytes * (StripeLayout.MaxStripeIndex + 1L),
        MaxConcatPartials * StripeLayout.MaxPartialLength(StripeSizeBytes, MaxBlocksPerStripe));
}
