using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Repositories;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Azure;
public class AzureBrokerStorageService : IBrokerStorageService
{
    private readonly IFileStore _fileStore;
    private readonly IResourceManager _resourceManager;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<AzureBrokerStorageService> _logger;

    public AzureBrokerStorageService(IFileStore fileStore, IResourceManager resourceManager, IHostEnvironment hostEnvironment, ILogger<AzureBrokerStorageService> logger)
    {
        _fileStore = fileStore;
        _resourceManager = resourceManager;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<string> UploadFile(ResourceOwnerEntity resourceOwnerEntity, FileEntity fileEntity, Stream stream, CancellationToken cancellationToken)
    {
        var connectionString = await GetConnectionString(resourceOwnerEntity);
        return await _fileStore.UploadFile(stream, fileEntity.FileId, connectionString, cancellationToken);
    }

    public async Task<Stream> DownloadFile(ResourceOwnerEntity resourceOwnerEntity, FileEntity fileEntity, CancellationToken cancellationToken)
    {
        var connectionString = await GetConnectionString(resourceOwnerEntity);
        return await _fileStore.GetFileStream(fileEntity.FileId, connectionString, cancellationToken);
    }

    public async Task DeleteFile(ResourceOwnerEntity resourceOwnerEntity, FileEntity fileEntity, CancellationToken cancellationToken)
    {
        var connectionString = await GetConnectionString(resourceOwnerEntity);
        await _fileStore.DeleteFile(fileEntity.FileId, connectionString, cancellationToken);
    }

    private async Task<string> GetConnectionString(ResourceOwnerEntity resourceOwnerEntity)
    {
        if (_hostEnvironment.IsDevelopment())
        {
            _logger.LogInformation("Running in development. Using local development storage.");
            return AzureConstants.AzuriteUrl;
        }
        return await _resourceManager.GetStorageConnectionString(resourceOwnerEntity);
    }
}
