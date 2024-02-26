using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.DownloadFileQuery;
public class DownloadFileQueryHandler : IHandler<DownloadFileQueryRequest, DownloadFileQueryResponse>
{
    private readonly IResourceRepository _resourceRepository;
    private readonly IResourceOwnerRepository _resourceOwnerRepository;
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IActorFileStatusRepository _actorFileStatusRepository;
    private readonly IBrokerStorageService _brokerStorageService;
    private readonly ILogger<DownloadFileQueryHandler> _logger;

    public DownloadFileQueryHandler(IResourceRepository resourceRepository, IResourceOwnerRepository resourceOwnerRepository, IAuthorizationService resourceRightsRepository, IFileRepository fileRepository, IActorFileStatusRepository actorFileStatusRepository, IBrokerStorageService brokerStorageService, ILogger<DownloadFileQueryHandler> logger)
    {
        _resourceRepository = resourceRepository;
        _resourceOwnerRepository = resourceOwnerRepository;
        _resourceRightsRepository = resourceRightsRepository;
        _fileRepository = fileRepository;
        _actorFileStatusRepository = actorFileStatusRepository;
        _brokerStorageService = brokerStorageService;
        _logger = logger;
    }

    public async Task<OneOf<DownloadFileQueryResponse, Error>> Process(DownloadFileQueryRequest request, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetFile(request.FileId, cancellationToken);
        if (file is null)
        {
            return Errors.FileNotFound;
        }
        if (file.FileStatusEntity.Status != FileStatus.Published)
        {
            return Errors.FileNotAvailable;
        }
        if (!file.RecipientCurrentStatuses.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Token.Consumer))
        {
            return Errors.FileNotFound;
        }
        if (string.IsNullOrWhiteSpace(file?.FileLocation))
        {
            return Errors.NoFileUploaded;
        }
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(file.ResourceId, request.Token.ClientId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var resource = await _resourceRepository.GetResource(file.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.ResourceNotConfigured;
        };
        var resourceOwner = await _resourceOwnerRepository.GetResourceOwner(resource.ResourceOwnerId);
        if (resourceOwner is null)
        {
            return Errors.ResourceOwnerNotConfigured;
        };
        var downloadStream = await _brokerStorageService.DownloadFile(resourceOwner, file, cancellationToken);
        await _actorFileStatusRepository.InsertActorFileStatus(request.FileId, ActorFileStatus.DownloadStarted, request.Token.Consumer, cancellationToken);
        return new DownloadFileQueryResponse()
        {
            Filename = file.Filename,
            Stream = downloadStream
        };
    }
}
