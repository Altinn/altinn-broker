using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.UploadFileCommand;

public class UploadFileCommandHandler : IHandler<UploadFileCommandRequest, Guid>
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IResourceManager _resourceManager;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly ILogger<UploadFileCommandHandler> _logger;

    public UploadFileCommandHandler(IServiceOwnerRepository serviceOwnerRepository, IFileRepository fileRepository, IResourceManager resourceMananger, IBrokerStorageService brokerStorageService, ILogger<UploadFileCommandHandler> logger)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileRepository = fileRepository;
        _resourceManager = resourceMananger;
        _brokerStorageService = brokerStorageService;
        _logger = logger;
    }

    public async Task<OneOf<Guid, ActionResult>> Process(UploadFileCommandRequest request)
    {
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Supplier);
        if (serviceOwner?.StorageProvider is null)
        {
            return new UnauthorizedObjectResult("Service owner not configured for the broker service");
        };
        var deploymentStatus = await _resourceManager.GetDeploymentStatus(serviceOwner);
        if (deploymentStatus != DeploymentStatus.Ready)
        {
            return new UnprocessableEntityObjectResult($"Service owner infrastructure is not ready. Status is: ${nameof(deploymentStatus)}");
        }
        var file = await _fileRepository.GetFileAsync(request.FileId);
        if (file is null)
        {
            return new NotFoundResult();
        }
        if (request.Consumer != file.Sender)
        {
            return new UnauthorizedObjectResult("You must use a bearer token that belongs to the sender");
        }

        await _fileRepository.InsertFileStatus(request.FileId, FileStatus.UploadStarted);
        await _brokerStorageService.UploadFile(serviceOwner, file, request.Filestream);
        await _fileRepository.SetStorageReference(request.FileId, serviceOwner.StorageProvider.Id, request.FileId.ToString());
        await _fileRepository.InsertFileStatus(request.FileId, FileStatus.UploadProcessing);
        // TODO, async jobs
        await _fileRepository.InsertFileStatus(request.FileId, FileStatus.Published);
        return file.FileId;
    }
}
