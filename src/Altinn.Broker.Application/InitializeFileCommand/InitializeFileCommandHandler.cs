using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

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

    public async Task<OneOf<Guid, Error>> Process(InitializeFileCommandRequest request)
    {
        if (request.Consumer != request.SenderExternalId)
        {
            return Errors.WrongTokenForSender;
        }
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Supplier);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        }
        var deploymentStatus = await _resourceManager.GetDeploymentStatus(serviceOwner);
        if (deploymentStatus != DeploymentStatus.Ready)
        {
            return Errors.ServiceOwnerNotReadyInfrastructure;
        }
        var fileId = await _fileRepository.AddFile(serviceOwner, request.Filename, request.SendersFileReference, request.SenderExternalId, request.RecipientExternalIds, request.PropertyList, request.Checksum);
        return fileId;
    }
}
