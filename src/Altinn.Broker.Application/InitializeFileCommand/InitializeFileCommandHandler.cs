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
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IActorFileStatusRepository _actorFileStatusRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<InitializeFileCommandHandler> _logger;

    public InitializeFileCommandHandler(
        IResourceRepository resourceRepository,
        IResourceOwnerRepository resourceOwnerRepository,
        IAuthorizationService resourceRightsRepository,
        IFileRepository fileRepository,
        IFileStatusRepository fileStatusRepository,
        IActorFileStatusRepository actorFileStatusRepository,
        IBackgroundJobClient backgroundJobClient,
        ILogger<InitializeFileCommandHandler> logger)
    {
        _resourceRepository = resourceRepository;
        _resourceOwnerRepository = resourceOwnerRepository;
        _resourceRightsRepository = resourceRightsRepository;
        _fileRepository = fileRepository;
        _fileStatusRepository = fileStatusRepository;
        _actorFileStatusRepository = actorFileStatusRepository;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task<OneOf<Guid, Error>> Process(InitializeFileCommandRequest request, CancellationToken ct)
    {
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(request.ResourceId, request.Token.ClientId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, request.IsLegacy, ct);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var resource = await _resourceRepository.GetResource(request.ResourceId, ct);
        if (resource is null)
        {
            return Errors.ResourceNotConfigured;
        };
        var resourceOwner = await _resourceOwnerRepository.GetResourceOwner(resource.ResourceOwnerId);
        if (resourceOwner?.StorageProvider is null)
        {
            return Errors.ResourceOwnerNotConfigured;
        }
        var fileId = await _fileRepository.AddFile(resourceOwner, resource, request.Filename, request.SendersFileReference, request.SenderExternalId, request.RecipientExternalIds, request.PropertyList, request.Checksum, null, ct);
        await _fileStatusRepository.InsertFileStatus(fileId, FileStatus.Initialized, ct: ct);
        var addRecipientEventTasks = request.RecipientExternalIds.Select(recipientId => _actorFileStatusRepository.InsertActorFileStatus(fileId, ActorFileStatus.Initialized, recipientId, ct));
        try
        {
            await Task.WhenAll(addRecipientEventTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed when adding recipient initialized events.");
        }
        _backgroundJobClient.Schedule<DeleteFileCommandHandler>((deleteFileCommandHandler) => deleteFileCommandHandler.Process(fileId, ct), resourceOwner.FileTimeToLive);

        return fileId;
    }
}
