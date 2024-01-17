using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;

using OneOf;

namespace Altinn.Broker.Application.GetFileDetailsQuery;

public class GetFileDetailsQueryHandler : IHandler<GetFileDetailsQueryRequest, GetFileDetailsQueryResponse>
{
    private readonly IFileRepository _fileRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IResourceOwnerRepository _resourceOwnerRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IActorFileStatusRepository _actorFileStatusRepository;

    public GetFileDetailsQueryHandler(IFileRepository fileRepository, IResourceRepository serviceRepositor, IResourceOwnerRepository resourceOwnerRepository, IFileStatusRepository fileStatusRepository, IActorFileStatusRepository actorFileStatusRepository)
    {
        _fileStatusRepository = fileStatusRepository;
        _actorFileStatusRepository = actorFileStatusRepository;
        _fileRepository = fileRepository;
        _resourceRepository = serviceRepositor;
        _resourceOwnerRepository = resourceOwnerRepository;
    }

    public async Task<OneOf<GetFileDetailsQueryResponse, Error>> Process(GetFileDetailsQueryRequest request)
    {
        var service = await _resourceRepository.GetResource(request.Token.ClientId);
        if (service is null)
        {
            return Errors.ResourceNotConfigured;
        };
        var resourceOwner = await _resourceOwnerRepository.GetResourceOwner(service.ResourceOwnerId);
        if (resourceOwner is null)
        {
            return Errors.ResourceOwnerNotConfigured;
        };
        var file = await _fileRepository.GetFile(request.FileId);
        if (file is null)
        {
            return Errors.FileNotFound;
        }
        if (file.Sender.ActorExternalId != request.Token.Consumer &&
            !file.RecipientCurrentStatuses.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Token.Consumer))
        {
            return Errors.FileNotFound;
        }
        var fileEvents = await _fileStatusRepository.GetFileStatusHistory(request.FileId);
        var actorEvents = await _actorFileStatusRepository.GetActorEvents(request.FileId);
        return new GetFileDetailsQueryResponse()
        {
            File = file,
            FileEvents = fileEvents,
            ActorEvents = actorEvents
        };
    }
}
