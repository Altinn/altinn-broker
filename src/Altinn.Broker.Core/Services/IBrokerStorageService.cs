using Altinn.Broker.Core.Domain;

/// <summary>
/// Handles the interplay of the ResourceOwnerEntity and the infrastructure resources we manage for them
/// </summary>
public interface IBrokerStorageService
{
    /// <summary>
    /// Looks up the correct storage account to use for service owner and upload the file
    /// </summary>
    /// <param name="resourceOwnerEntity">The resource owner entity.</param>
    /// <param name="stream">The stream to upload.</param>
    /// <returns>A string containing the MD5 checksum</returns>
    Task<string> UploadFile(ResourceOwnerEntity resourceOwnerEntity, FileEntity fileEntity, Stream stream);

    Task<Stream> DownloadFile(ResourceOwnerEntity resourceOwnerEntity, FileEntity file);
    Task DeleteFile(ResourceOwnerEntity resourceOwnerEntity, FileEntity fileEntity);
}
