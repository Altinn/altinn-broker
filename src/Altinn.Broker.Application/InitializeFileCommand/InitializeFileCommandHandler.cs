using Altinn.Broker.Application.DeleteFileCommand;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Hangfire;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.InitializeFileCommand;
public class InitializeFileCommandHandler : IHandler<InitializeFileCommandRequest, Guid>
{
    private readonly IResourceRepository _resourceRepository;
    private readonly IResourceOwnerRepository _resourceOwnerRepository;
    private readonly IResourceRightsRepository _resourceRightsRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IActorFileStatusRepository _actorFileStatusRepository;
    private readonly IResourceManager _resourceManager;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<InitializeFileCommandHandler> _logger;

    public InitializeFileCommandHandler(
        IResourceRepository resourceRepository,
        IResourceOwnerRepository resourceOwnerRepository,
        IResourceRightsRepository resourceRightsRepository,
        IFileRepository fileRepository,
        IFileStatusRepository fileStatusRepository,
        IActorFileStatusRepository actorFileStatusRepository,
        IResourceManager resourceManager,
        IBackgroundJobClient backgroundJobClient,
        ILogger<InitializeFileCommandHandler> logger)
    {
        _resourceRepository = resourceRepository;
        _resourceOwnerRepository = resourceOwnerRepository;
        _resourceRightsRepository = resourceRightsRepository;
        _fileRepository = fileRepository;
        _fileStatusRepository = fileStatusRepository;
        _actorFileStatusRepository = actorFileStatusRepository;
        _resourceManager = resourceManager;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task<OneOf<Guid, Error>> Process(InitializeFileCommandRequest request)
    {
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(request.ResourceId, request.Token.ClientId, ResourceAccessLevel.Write, request.IsLegacy);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        if (request.Token.Consumer != request.SenderExternalId)
        {
            return Errors.NoAccessToResource;
        }
        var resource = await _resourceRepository.GetResource(request.ResourceId);
        if (resource is null)
        {
            return Errors.ResourceNotConfigured;
        };
        var resourceOwner = await _resourceOwnerRepository.GetResourceOwner(resource.ResourceOwnerId);
        if (resourceOwner?.StorageProvider is null)
        {
            return Errors.ResourceOwnerNotConfigured;
        }
        var fileId = await _fileRepository.AddFile(resourceOwner, resource, request.Filename, request.SendersFileReference, request.SenderExternalId, request.RecipientExternalIds, request.PropertyList, request.Checksum, null);
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
        _backgroundJobClient.Schedule<DeleteFileCommandHandler>((deleteFileCommandHandler) => deleteFileCommandHandler.Process(fileId), resourceOwner.FileTimeToLive);

        return fileId;
    }
}
