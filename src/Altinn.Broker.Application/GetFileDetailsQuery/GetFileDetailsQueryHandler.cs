using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;

using Microsoft.AspNetCore.Mvc;

using OneOf;

namespace Altinn.Broker.Application.GetFileDetailsQuery;

public class GetFileDetailsQueryHandler : IHandler<GetFileDetailsQueryRequest, GetFileDetailsQueryResponse>
{
    private readonly IFileRepository _fileRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;

    public GetFileDetailsQueryHandler(IFileRepository fileRepository, IServiceOwnerRepository serviceOwnerRepository)
    {
        _fileRepository = fileRepository;
        _serviceOwnerRepository = serviceOwnerRepository;
    }

    public async Task<OneOf<GetFileDetailsQueryResponse, ActionResult>> Process(GetFileDetailsQueryRequest request)
    {
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Supplier);
        if (serviceOwner is null)
        {
            return new UnauthorizedObjectResult("Service owner not configured for the broker service");
        };
        var file = await _fileRepository.GetFileAsync(request.FileId);
        if (file is null)
        {
            return new NotFoundResult();
        }
        if (!file.ActorEvents.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Consumer))
        {
            return new NotFoundResult();
        }
        var fileEvents = await _fileRepository.GetFileStatusHistoryAsync(request.FileId);
        var actorEvents = await _fileRepository.GetActorEvents(request.FileId);
        return new GetFileDetailsQueryResponse()
        {
            File = file,
            FileEvents = fileEvents,
            ActorEvents = actorEvents
        };
    }
}
