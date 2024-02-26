using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFilesQuery;

public class GetFilesQueryHandler : IHandler<GetFilesQueryRequest, List<Guid>>
{
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IActorRepository _actorRepository;
    private readonly ILogger<GetFilesQueryHandler> _logger;

    public GetFilesQueryHandler(IAuthorizationService resourceRightsRepository, IResourceRepository resourceRepository, IFileRepository fileRepository, IActorRepository actorRepository, ILogger<GetFilesQueryHandler> logger)
    {
        _resourceRightsRepository = resourceRightsRepository;
        _resourceRepository = resourceRepository;
        _fileRepository = fileRepository;
        _actorRepository = actorRepository;
        _logger = logger;
    }

    public async Task<OneOf<List<Guid>, Error>> Process(GetFilesQueryRequest request, CancellationToken ct)
    {
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(request.ResourceId, request.Token.ClientId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write, ResourceAccessLevel.Read }, ct: ct);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var service = await _resourceRepository.GetResource(request.ResourceId, ct);
        if (service is null)
        {
            return Errors.ResourceNotConfigured;
        };
        var callingActor = await _actorRepository.GetActorAsync(request.Token.Consumer, ct);
        if (callingActor is null)
        {
            return new List<Guid>();
        }

        FileSearchEntity fileSearchEntity = new()
        {
            Actor = callingActor,
            ResourceId = request.ResourceId,
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
            return await _fileRepository.GetFilesForRecipientWithRecipientStatus(fileSearchEntity, ct);
        }
        else
        {
            return await _fileRepository.GetFilesAssociatedWithActor(fileSearchEntity, ct);
        }
    }
}
