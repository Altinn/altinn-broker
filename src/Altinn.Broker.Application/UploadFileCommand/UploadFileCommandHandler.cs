using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.UploadFileCommand;

public class UploadFileCommandHandler : IHandler<UploadFileCommandRequest, Guid>
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IResourceManager _resourceManager;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly ILogger<UploadFileCommandHandler> _logger;

    public UploadFileCommandHandler(IServiceOwnerRepository serviceOwnerRepository, IFileRepository fileRepository, IFileStatusRepository fileStatusRepository, IResourceManager resourceMananger, IBrokerStorageService brokerStorageService, ILogger<UploadFileCommandHandler> logger)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileRepository = fileRepository;
        _fileStatusRepository = fileStatusRepository;
        _resourceManager = resourceMananger;
        _brokerStorageService = brokerStorageService;
        _logger = logger;
    }

    public async Task<OneOf<Guid, Error>> Process(UploadFileCommandRequest request)
    {
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Supplier);
        if (serviceOwner?.StorageProvider is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        };
        var deploymentStatus = await _resourceManager.GetDeploymentStatus(serviceOwner);
        if (deploymentStatus != DeploymentStatus.Ready)
        {
            return Errors.ServiceOwnerNotReadyInfrastructure;
        }
        var file = await _fileRepository.GetFile(request.FileId);
        if (file is null)
        {
            return Errors.FileNotFound;
        }
        if (request.Consumer != file.Sender)
        {
            return Errors.FileNotFound;
        }

        await _fileStatusRepository.InsertFileStatus(request.FileId, FileStatus.UploadStarted);
        await _brokerStorageService.UploadFile(serviceOwner, file, request.Filestream);
        await _fileRepository.SetStorageReference(request.FileId, serviceOwner.StorageProvider.Id, request.FileId.ToString());
        await _fileStatusRepository.InsertFileStatus(request.FileId, FileStatus.UploadProcessing);
        // TODO, async jobs
        await _fileStatusRepository.InsertFileStatus(request.FileId, FileStatus.Published);
        return file.FileId;
    }
}
