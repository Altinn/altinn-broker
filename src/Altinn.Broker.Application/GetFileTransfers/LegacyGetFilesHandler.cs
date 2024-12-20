using System.Security.Claims;

using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileTransfers;

public class LegacyGetFilesHandler(IFileTransferRepository fileTransferRepository, IActorRepository actorRepository, ILogger<GetFileTransfersHandler> logger) : IHandler<LegacyGetFilesRequest, List<Guid>>
{
    private async Task<List<ActorEntity>> GetActors(string[] recipients, CancellationToken cancellationToken)
    {
        List<ActorEntity> actors = new();
        foreach (string recipient in recipients)
        {
            var entity = await actorRepository.GetActorAsync(recipient, cancellationToken);
            if (entity is not null)
            {
                actors.Add(entity);
            }
        }

        return actors;
    }

    public async Task<OneOf<List<Guid>, Error>> Process(LegacyGetFilesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Legacy get files for {resourceId}", request.ResourceId is null ? "all resources" : request.ResourceId.SanitizeForLogs());
        LegacyFileSearchEntity fileSearch = new()
        {
            ResourceId = request.ResourceId ?? string.Empty
        };
        // TODO: should we just call GetFiles for each recipient or should we gather everything into 1 single SQL request.
        if (request.Recipients?.Length > 0)
        {
            fileSearch.Actors = await GetActors(request.Recipients, cancellationToken);
        }
        else
        {
            fileSearch.Actor = await actorRepository.GetActorAsync(request.OnBehalfOfConsumer ?? string.Empty, cancellationToken);
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

        if (request.RecipientFileTransferStatus.HasValue)
        {
            fileSearch.RecipientFileTransferStatus = request.RecipientFileTransferStatus;
        }

        if (request.FileTransferStatus.HasValue)
        {
            fileSearch.FileTransferStatus = request.FileTransferStatus;
        }

        return await fileTransferRepository.LegacyGetFilesForRecipientsWithRecipientStatus(fileSearch, cancellationToken);
    }
}
