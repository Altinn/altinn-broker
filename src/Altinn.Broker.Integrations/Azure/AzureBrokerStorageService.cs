using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Repositories;

namespace Altinn.Broker.Integrations.Azure;
public class AzureBrokerStorageService : IBrokerStorageService
{
    private readonly IFileStore _fileStore;
    private readonly IResourceManager _resourceManager;

    public AzureBrokerStorageService(IFileStore fileStore, IResourceManager resourceManager)
    {
        _fileStore = fileStore;
        _resourceManager = resourceManager;
    }

    public async Task UploadFile(ServiceOwnerEntity? serviceOwnerEntity, FileEntity fileEntity, Stream stream)
    {
        var connectionString = await _resourceManager.GetStorageConnectionString(serviceOwnerEntity);
        await _fileStore.UploadFile(stream, fileEntity.FileId, connectionString);
    }

    public async Task<Stream> DownloadFile(ServiceOwnerEntity? serviceOwnerEntity, FileEntity fileEntity)
    {
        var connectionString = await _resourceManager.GetStorageConnectionString(serviceOwnerEntity);
        return await _fileStore.GetFileStream(fileEntity.FileId, connectionString);
    }
}
