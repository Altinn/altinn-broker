using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Repositories;

using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Azure;
public class AzureBrokerStorageService : IBrokerStorageService
{
    private readonly IFileStore _fileStore;
    private readonly IResourceManager _resourceManager;
    private readonly AzureStorageOptions _azureStorageOptions;

    public AzureBrokerStorageService(IFileStore fileStore, IResourceManager resourceManager, IOptions<AzureStorageOptions> options)
    {
        _fileStore = fileStore;
        _resourceManager = resourceManager;
        _azureStorageOptions = options.Value;
    }

    public async Task UploadFile(ServiceOwnerEntity serviceOwnerEntity, FileEntity fileEntity, Stream stream)
    {
        var connectionString = await GetConnectionString(serviceOwnerEntity);
        await _fileStore.UploadFile(stream, fileEntity.FileId, connectionString);
    }

    public async Task<Stream> DownloadFile(ServiceOwnerEntity serviceOwnerEntity, FileEntity fileEntity)
    {
        var connectionString = await GetConnectionString(serviceOwnerEntity);
        return await _fileStore.GetFileStream(fileEntity.FileId, connectionString);
    }

    private async Task<string> GetConnectionString(ServiceOwnerEntity serviceOwnerEntity)
    {
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            return _azureStorageOptions.ConnectionString;
        }
        return await _resourceManager.GetStorageConnectionString(serviceOwnerEntity);
    }
}
