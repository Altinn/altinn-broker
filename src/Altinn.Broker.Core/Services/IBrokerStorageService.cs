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
    /// <returns>A string containing the MD5 checksum</returns>
    Task<string> UploadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity, Stream stream, CancellationToken cancellationToken);

    Task<Stream> DownloadFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransfer, CancellationToken cancellationToken);
    Task DeleteFile(ServiceOwnerEntity serviceOwnerEntity, FileTransferEntity fileTransferEntity, CancellationToken cancellationToken);
}
