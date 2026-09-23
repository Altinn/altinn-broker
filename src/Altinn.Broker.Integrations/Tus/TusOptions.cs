namespace Altinn.Broker.Integrations.Tus;

public class TusOptions
{
    public const string SectionName = "TusOptions";

    /// <summary>
    /// Default maximum size of a single TUS PATCH chunk (100 MiB).
    /// Chunks are buffered in memory while staging to Azure, so larger values risk OOM.
    /// </summary>
    public const long DefaultMaxChunkSizeBytes = 100L * 1024 * 1024;

    /// <summary>
    /// How long an incomplete TUS upload may remain resumable before expiration.
    /// </summary>
    public TimeSpan UploadExpiration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Maximum size of a single TUS PATCH request body in bytes.
    /// Requests that exceed this limit are rejected before the full body is buffered.
    /// </summary>
    public long MaxChunkSizeBytes { get; set; } = DefaultMaxChunkSizeBytes;
}
