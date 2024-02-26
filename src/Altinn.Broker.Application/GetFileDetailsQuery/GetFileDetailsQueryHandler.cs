using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using OneOf;

namespace Altinn.Broker.Application.GetFileDetailsQuery;

public class GetFileDetailsQueryHandler : IHandler<GetFileDetailsQueryRequest, GetFileDetailsQueryResponse>
{
    private readonly IFileRepository _fileRepository;
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IActorFileStatusRepository _actorFileStatusRepository;

    public GetFileDetailsQueryHandler(IFileRepository fileRepository, IAuthorizationService resourceRightsRepository, IFileStatusRepository fileStatusRepository, IActorFileStatusRepository actorFileStatusRepository)
    {
        _fileStatusRepository = fileStatusRepository;
        _actorFileStatusRepository = actorFileStatusRepository;
        _fileRepository = fileRepository;
        _resourceRightsRepository = resourceRightsRepository;
    }

    public async Task<OneOf<GetFileDetailsQueryResponse, Error>> Process(GetFileDetailsQueryRequest request, CancellationToken ct)
    {
        var file = await _fileRepository.GetFile(request.FileId, ct);
        if (file is null)
        {
            return Errors.FileNotFound;
        }
        if (file.Sender.ActorExternalId != request.Token.Consumer &&
            !file.RecipientCurrentStatuses.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Token.Consumer))
        {
            return Errors.FileNotFound;
        }
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(file.ResourceId, request.Token.ClientId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write, ResourceAccessLevel.Read }, ct: ct);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var fileEvents = await _fileStatusRepository.GetFileStatusHistory(request.FileId, ct);
        var actorEvents = await _actorFileStatusRepository.GetActorEvents(request.FileId, ct);
        return new GetFileDetailsQueryResponse()
        {
            File = file,
            FileEvents = fileEvents,
            ActorEvents = actorEvents
        };
    }
}
