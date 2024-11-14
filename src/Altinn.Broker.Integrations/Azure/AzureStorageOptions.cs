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
}
