using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileOverviewQuery;

public class GetFileOverviewQueryHandler : IHandler<GetFileOverviewQueryRequest, GetFileOverviewQueryResponse>
{
    private readonly IResourceOwnerRepository _resourceOwnerRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<GetFileOverviewQueryHandler> _logger;

    public GetFileOverviewQueryHandler(IResourceOwnerRepository resourceOwnerRepository, IResourceRepository serviceRepositor, IFileRepository fileRepository, IResourceManager resourceManager, ILogger<GetFileOverviewQueryHandler> logger)
    {
        _resourceOwnerRepository = resourceOwnerRepository;
        _resourceRepository = serviceRepositor;
        _fileRepository = fileRepository;
        _logger = logger;
    }

    public async Task<OneOf<GetFileOverviewQueryResponse, Error>> Process(GetFileOverviewQueryRequest request)
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
        return new GetFileOverviewQueryResponse()
        {
            File = file
        };
    }
}
