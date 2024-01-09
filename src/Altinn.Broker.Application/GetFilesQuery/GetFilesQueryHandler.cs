using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFilesQuery;

public class GetFilesQueryHandler : IHandler<GetFilesQueryRequest, List<Guid>>
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IActorRepository _actorRepository;
    private readonly ILogger<GetFilesQueryHandler> _logger;

    public GetFilesQueryHandler(IServiceOwnerRepository serviceOwnerRepository, IServiceRepository serviceRepository, IFileRepository fileRepository, IActorRepository actorRepository, ILogger<GetFilesQueryHandler> logger)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _serviceRepository = serviceRepository;
        _fileRepository = fileRepository;
        _actorRepository = actorRepository;
        _logger = logger;
    }

    public async Task<OneOf<List<Guid>, Error>> Process(GetFilesQueryRequest request)
    {
        var service = await _serviceRepository.GetService(request.Token.ClientId);
        if (service is null)
        {
            return Errors.ServiceNotConfigured;
        };
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Token.Supplier);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        };
        var callingActor = await _actorRepository.GetActorAsync(request.Token.Consumer);
        if (callingActor is null)
        {
            return new List<Guid>();
        }

        FileSearchEntity fileSearchEntity = new()
        {
            Actor = callingActor,
            Status = request.Status
        };

        if (request.From.HasValue)
        {
            fileSearchEntity.From = new DateTimeOffset(request.From.Value.UtcDateTime, TimeSpan.Zero);
        }

        if (request.To.HasValue)
        {
            fileSearchEntity.To = new DateTimeOffset(request.To.Value.UtcDateTime, TimeSpan.Zero);
        }

        var files = await _fileRepository.GetFilesAssociatedWithActor(fileSearchEntity);
        return files;
    }
}
