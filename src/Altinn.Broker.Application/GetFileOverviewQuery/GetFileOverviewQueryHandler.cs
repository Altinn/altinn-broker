using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileOverviewQuery;

public class GetFileOverviewQueryHandler : IHandler<GetFileOverviewQueryRequest, GetFileOverviewQueryResponse>
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<GetFileOverviewQueryHandler> _logger;

    public GetFileOverviewQueryHandler(IServiceOwnerRepository serviceOwnerRepository, IServiceRepository serviceRepositor, IFileRepository fileRepository, IResourceManager resourceManager, ILogger<GetFileOverviewQueryHandler> logger)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _serviceRepository = serviceRepositor;
        _fileRepository = fileRepository;
        _logger = logger;
    }

    public async Task<OneOf<GetFileOverviewQueryResponse, Error>> Process(GetFileOverviewQueryRequest request)
    {
        var service = await _serviceRepository.GetService(request.Token.ClientId);
        if (service is null)
        {
            return Errors.ServiceNotConfigured;
        };
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(service.ServiceOwnerId);
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
        return new GetFileOverviewQueryResponse()
        {
            File = file
        };
    }
}
