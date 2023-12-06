using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileOverviewQuery;

public class GetFileOverviewQueryHandler : IHandler<GetFileOverviewQueryRequest, GetFileOverviewQueryResponse>
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<GetFileOverviewQueryHandler> _logger;

    public GetFileOverviewQueryHandler(IServiceOwnerRepository serviceOwnerRepository, IFileRepository fileRepository, IResourceManager resourceManager, ILogger<GetFileOverviewQueryHandler> logger)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileRepository = fileRepository;
        _logger = logger;
    }

    public async Task<OneOf<GetFileOverviewQueryResponse, ActionResult>> Process(GetFileOverviewQueryRequest request)
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
        return new GetFileOverviewQueryResponse()
        {
            File = file
        };
    }
}
