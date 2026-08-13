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
    /// Maximum number of blobs a single file transfer's content may be spread over.
    /// Together with <see cref="MaxStripeBytes"/> this sets the largest transfer Broker can store.
    /// </summary>
    public int MaxStripes { get; set; } = 16;

    /// <summary>
    /// Smallest stripe size, so a resource with a modest size limit is never striped at all.
    /// A transfer only spans several blobs once it grows past this.
    /// </summary>
    public long MinStripeBytes { get; set; } = 274_877_906_944; // 256 GiB

    /// <summary>
    /// Largest stripe size. Set so that a full stripe never requires a block larger than the 100 MB
    /// Azure accepts (<see cref="MaxBlocksPerStripe"/> x 100 MB).
    /// </summary>
    public long MaxStripeBytes { get; set; } = 5_000_000_000_000;

    /// <summary>
    /// Blocks a single stripe blob may hold. Azure's hard limit is 50 000; it is configurable only so
    /// tests can shrink it and exercise the budget without moving large amounts of data.
    /// </summary>
    public int MaxBlocksPerStripe { get; set; } = 50_000;

    /// <summary>
    /// The largest file transfer that can be stored, and therefore the ceiling for a resource's
    /// configured maximum transfer size.
    /// </summary>
    public long MaxTotalTransferBytes => MaxStripeBytes * MaxStripes;
}
