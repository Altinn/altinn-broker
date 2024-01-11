using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFilesQuery;

public class GetFilesQueryHandler : IHandler<GetFilesQueryRequest, List<Guid>>
{
    private readonly IServiceOwnerRepository _serviceOwnerRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IActorRepository _actorRepository;
    private readonly ILogger<GetFilesQueryHandler> _logger;

    public GetFilesQueryHandler(IServiceOwnerRepository serviceOwnerRepository, IFileRepository fileRepository, IActorRepository actorRepository, ILogger<GetFilesQueryHandler> logger)
    {
        _serviceOwnerRepository = serviceOwnerRepository;
        _fileRepository = fileRepository;
        _actorRepository = actorRepository;
        _logger = logger;
    }

    public async Task<OneOf<List<Guid>, Error>> Process(GetFilesQueryRequest request)
    {
        var serviceOwner = await _serviceOwnerRepository.GetServiceOwner(request.Supplier);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        };
        var callingActor = await _actorRepository.GetActorAsync(request.Consumer);
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

        if (request.RecipientStatus.HasValue)
        {
            fileSearchEntity.RecipientStatus = request.RecipientStatus;
            return await _fileRepository.GetFilesForRecipientWithRecipientStatus(fileSearchEntity);
        }
        else
        {
            return await _fileRepository.GetFilesAssociatedWithActor(fileSearchEntity);
        }        
    }
}
