using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Services;
using Altinn.Broker.Repositories;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Integrations.Azure;
public class AzureBrokerStorageService : IBrokerStorageService
{
    private readonly IFileStore _fileStore;
    private readonly IResourceManager _resourceManager;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly AzureStorageOptions _azureStorageOptions;
    private readonly ILogger<AzureBrokerStorageService> _logger;

    public AzureBrokerStorageService(IFileStore fileStore, IResourceManager resourceManager, IHostEnvironment hostEnvironment, IOptions<AzureStorageOptions> options, ILogger<AzureBrokerStorageService> logger)
    {
        _fileStore = fileStore;
        _resourceManager = resourceManager;
        _hostEnvironment = hostEnvironment;
        _azureStorageOptions = options.Value;
        _logger = logger;
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
        if (_hostEnvironment.IsDevelopment())
        {
            _logger.LogInformation("Running in development. Using local development storage.");
            return _azureStorageOptions.ConnectionString;
        }
        return await _resourceManager.GetStorageConnectionString(serviceOwnerEntity);
    }
}
