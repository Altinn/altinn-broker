using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFilesQuery;

public class GetFilesQueryHandler : IHandler<GetFilesQueryRequest, List<Guid>>
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<GetFilesQueryHandler> _logger;

    public GetFilesQueryHandler(IServiceOwnerRepository serviceOwnerRepository, IFileRepository fileRepository, ILogger<GetFilesQueryHandler> logger)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileRepository = fileRepository;
        _logger = logger;
    }

    public async Task<OneOf<List<Guid>, ActionResult>> Process(GetFilesQueryRequest request)
    {
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Supplier);
        if (serviceOwner is null)
        {
            return new UnauthorizedObjectResult("Service owner not configured for the broker service");
        };
        var files = await _fileRepository.GetFilesAvailableForCaller(request.Consumer);
        return files;
    }
}
