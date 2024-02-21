using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFilesQuery;

public class LegacyGetFilesQueryHandler : IHandler<LegacyGetFilesQueryRequest, List<Guid>>
{
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IActorRepository _actorRepository;
    private readonly ILogger<GetFilesQueryHandler> _logger;

    public LegacyGetFilesQueryHandler(IAuthorizationService resourceRightsRepository, IResourceRepository resourceRepository, IFileRepository fileRepository, IActorRepository actorRepository, ILogger<GetFilesQueryHandler> logger)
    {
        _resourceRightsRepository = resourceRightsRepository;
        _resourceRepository = resourceRepository;
        _fileRepository = fileRepository;
        _actorRepository = actorRepository;
        _logger = logger;
    }

    private async Task<List<ActorEntity>> GetActors(string[] recipients)
    {
        List<ActorEntity> actors = new();
        foreach (string recipient in recipients)
        {
            ActorEntity entity = await _actorRepository.GetActorAsync(recipient);
            actors.Add(entity);
        }

        return actors;
    }

    public async Task<OneOf<List<Guid>, Error>> Process(LegacyGetFilesQueryRequest request)
    {
        LegacyFileSearchEntity fileSearch = new()
        {
            ResourceId = request.ResourceId ?? string.Empty
        };
        // TODO: should we just call GetFiles for each recipient or should we gather everything into 1 single SQL request.
        if (request.Recipients?.Length > 0)
        {
            fileSearch.Actors = await GetActors(request.Recipients);
        }
        else
        {
            fileSearch.Actor = await _actorRepository.GetActorAsync(request.OnBehalfOfConsumer ?? string.Empty);
            if (fileSearch.Actor is null)
            {
                return new List<Guid>();
            }
        }

        if (request.From.HasValue)
        {
            fileSearch.From = new DateTimeOffset(request.From.Value.UtcDateTime, TimeSpan.Zero);
        }

        if (request.To.HasValue)
        {
            fileSearch.To = new DateTimeOffset(request.To.Value.UtcDateTime, TimeSpan.Zero);
        }

        if (request.RecipientStatus.HasValue)
        {
            fileSearch.RecipientStatus = request.RecipientStatus;
        }

        return await _fileRepository.LegacyGetFilesForRecipientsWithRecipientStatus(fileSearch);
    }
}
