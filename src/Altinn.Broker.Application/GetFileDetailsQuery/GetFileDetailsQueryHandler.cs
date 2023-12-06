using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;

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

    public async Task<OneOf<GetFileDetailsQueryResponse, Error>> Process(GetFileDetailsQueryRequest request)
    {
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Supplier);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        };
        var file = await _fileRepository.GetFile(request.FileId);
        if (file is null)
        {
            return Errors.FileNotFound;
        }
        if (!file.ActorEvents.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Consumer))
        {
            return Errors.FileNotFound;
        }
        var fileEvents = await _fileRepository.GetFileStatusHistory(request.FileId);
        var actorEvents = await _fileRepository.GetActorEvents(request.FileId);
        return new GetFileDetailsQueryResponse()
        {
            File = file,
            FileEvents = fileEvents,
            ActorEvents = actorEvents
        };
    }
}
