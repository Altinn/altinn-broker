using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;

using OneOf;

namespace Altinn.Broker.Application.GetFileDetailsQuery;

public class GetFileDetailsQueryHandler : IHandler<GetFileDetailsQueryRequest, GetFileDetailsQueryResponse>
{
    private readonly IFileRepository _fileRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileStatusRepository _fileStatusRepository;
    private readonly IActorFileStatusRepository _actorFileStatusRepository;

    public GetFileDetailsQueryHandler(IFileRepository fileRepository, IServiceRepository serviceRepositor, IServiceOwnerRepository serviceOwnerRepository, IFileStatusRepository fileStatusRepository, IActorFileStatusRepository actorFileStatusRepository)
    {
        _fileStatusRepository = fileStatusRepository;
        _actorFileStatusRepository = actorFileStatusRepository;
        _fileRepository = fileRepository;
        _serviceRepository = serviceRepositor;
        _serviceOwnerRepository = serviceOwnerRepository;
    }

    public async Task<OneOf<GetFileDetailsQueryResponse, Error>> Process(GetFileDetailsQueryRequest request)
    {
        var service = await _serviceRepository.GetService(request.Token.Consumer);
        if (service is null)
        {
            return Errors.ServiceNotConfigured;
        };
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Token.Supplier);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
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
