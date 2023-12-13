using Altinn.Broker.Core.Domain;

/// <summary>
/// Handles the interplay of the ServiceOwnerEntity and the infrastructure resources we manage for them
/// </summary>
public interface IBrokerStorageService
{
    /// <summary>
    /// Looks up the correct storage account to use for service owner and upload the file
    /// </summary>
    /// <param name="serviceOwnerEntity"></param>
    /// <param name="stream"></param>
    /// <returns></returns>
    Task UploadFile(ServiceOwnerEntity serviceOwnerEntity, FileEntity fileEntity, Stream stream);

    Task<Stream> DownloadFile(ServiceOwnerEntity serviceOwnerEntity, FileEntity file);
    Task DeleteFile(ServiceOwnerEntity serviceOwnerEntity, FileEntity fileEntity);
}
