using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileOverviewQuery;

public class GetFileOverviewQueryHandler : IHandler<GetFileOverviewQueryRequest, GetFileOverviewQueryResponse>
{
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<GetFileOverviewQueryHandler> _logger;

    public GetFileOverviewQueryHandler(IResourceOwnerRepository resourceOwnerRepository, IAuthorizationService resourceRightsRepository, IFileRepository fileRepository, IResourceManager resourceManager, ILogger<GetFileOverviewQueryHandler> logger)
    {
        _resourceRightsRepository = resourceRightsRepository;
        _fileRepository = fileRepository;
        _logger = logger;
    }

    public async Task<OneOf<GetFileOverviewQueryResponse, Error>> Process(GetFileOverviewQueryRequest request)
    {
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
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(file.ResourceId, request.Token.ClientId, ResourceAccessLevel.Write, request.IsLegacy)
                     || await _resourceRightsRepository.CheckUserAccess(file.ResourceId, request.Token.ClientId, ResourceAccessLevel.Read, request.IsLegacy);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        return new GetFileOverviewQueryResponse()
        {
            File = file
        };
    }
}
