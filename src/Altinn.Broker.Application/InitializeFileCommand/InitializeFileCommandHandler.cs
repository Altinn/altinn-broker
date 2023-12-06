using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.InitializeFileCommand;
public class InitializeFileCommandHandler : IHandler<InitializeFileCommandRequest, Guid>
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IResourceManager _resourceManager;
    private readonly ILogger<InitializeFileCommandHandler> _logger;

    public InitializeFileCommandHandler(IServiceOwnerRepository serviceOwnerRepository, IFileRepository fileRepository, IResourceManager resourceManager, ILogger<InitializeFileCommandHandler> logger)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileRepository = fileRepository;
        _resourceManager = resourceManager;
        _logger = logger;
    }

    public async Task<OneOf<Guid, ActionResult>> Process(InitializeFileCommandRequest request)
    {
        if (request.Consumer != request.SenderExternalId)
        {
            return new UnauthorizedObjectResult("You must use a bearer token that belongs to the sender");
        }
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Supplier);
        if (serviceOwner is null)
        {
            return new BadRequestObjectResult("Service owner needs to be configured to use the broker API");
        }
        var deploymentStatus = await _resourceManager.GetDeploymentStatus(serviceOwner);
        if (deploymentStatus != DeploymentStatus.Ready)
        {
            return new UnprocessableEntityObjectResult($"Service owner infrastructure is not ready. Status is: ${nameof(deploymentStatus)}");
        }
        var fileId = await _fileRepository.AddFile(serviceOwner, request.Filename, request.SendersFileReference, request.SenderExternalId, request.RecipientIds, request.PropertyList, request.Checksum);
        return fileId;
    }
}
