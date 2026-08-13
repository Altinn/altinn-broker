using Altinn.Broker.Core.Domain;

/// <summary>
/// Handles the interplay of the ServiceOwnerEntity and the infrastructure resources we manage for them
/// </summary>
public interface IBrokerStorageService
{
    /// <summary>
    /// Looks up the correct storage account to use for service owner and upload the file
    /// </summary>
    /// <param name="serviceOwnerEntity">The service owner entity.</param>
    /// <param name="stream">The stream to upload.</param>
    /// <returns>A string containing the MD5 checksum. Null if failure.</returns>
    Task<(string? Checksum, long Length)?> UploadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity, Stream stream, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the file, optionally restricted to a byte range so that only the requested bytes are fetched from storage.
    /// </summary>
    Task<BrokerFileDownload> DownloadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransfer, ByteRange? range, CancellationToken cancellationToken);
    Task DeleteFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity, CancellationToken cancellationToken);
    Task SetContentHashForExistingBlob(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity, CancellationToken cancellationToken);
    Task<string?> ComputeFileChecksumAsync(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity, CancellationToken cancellationToken);
    Task<string> UploadReportFileToStorage(string fileName, Stream stream, CancellationToken cancellationToken);
    /// <summary>
    /// Promotes a completed TUS upload to its final location. Returns null if it cannot be found, and
    /// a null StripeSizeBytes when the content ended up in a single blob.
    /// </summary>
    Task<(string? Checksum, long Length, long? StripeSizeBytes)?> FinalizeTusUpload(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity, CancellationToken cancellationToken);
}
