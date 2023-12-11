using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.InitializeFileCommand;
public class InitializeFileCommandHandler : IHandler<InitializeFileCommandRequest, Guid>
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IActorFileStatusRepository _actorFileStatusRepository;
    private readonly IResourceManager _resourceManager;
    private readonly ILogger<InitializeFileCommandHandler> _logger;

    public InitializeFileCommandHandler(IServiceOwnerRepository serviceOwnerRepository, IFileRepository fileRepository, IFileStatusRepository fileStatusRepository, IActorFileStatusRepository actorFileStatusRepository, IResourceManager resourceManager, ILogger<InitializeFileCommandHandler> logger)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileRepository = fileRepository;
        _fileStatusRepository = fileStatusRepository;
        _actorFileStatusRepository = actorFileStatusRepository;
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
        await _fileStatusRepository.InsertFileStatus(fileId, FileStatus.Initialized);
        var addRecipientEventTasks = request.RecipientExternalIds.Select(recipientId => _actorFileStatusRepository.InsertActorFileStatus(fileId, ActorFileStatus.Initialized, recipientId));
        try
        {
            await Task.WhenAll(addRecipientEventTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed when adding recipient initialized events.");
        }
        return fileId;
    }
}
