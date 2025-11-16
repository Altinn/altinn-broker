using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

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
            logger.LogInformation("Getting actors for specified recipients: {recipients}", string.Join(',', request.Recipients).SanitizeForLogs());
            fileSearch.Actors = await GetActors(request.Recipients, cancellationToken);
            logger.LogInformation("Got actors for specified recipients: {recipients}", string.Join(',', request.Recipients).SanitizeForLogs());
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

        logger.LogInformation("Searching for files with constructed search criteria: {fileSearch}", JsonSerializer.Serialize(fileSearch));

        var sw1 = Stopwatch.StartNew();
        var sw2 = Stopwatch.StartNew();

        var task1 = Task.Run(async () =>
        {
            var result = await fileTransferRepository.LegacyGetFilesForRecipientsWithRecipientStatus(fileSearch, cancellationToken);
            sw1.Stop();
            return result;
        });

        var task2 = Task.Run(async () =>
        {
            var result = await fileTransferRepository.LegacyGetFilesForRecipientsWithRecipientStatusDenormalized(fileSearch, cancellationToken);
            sw2.Stop();
            return result;
        });

        var results = await Task.WhenAll(task1, task2);
        var fileTransfers = results[0];
        var fileTransfersFromDenormalized = results[1];

        logger.LogInformation("Query performance - Original: {originalMs}ms, Denormalized: {denormalizedMs}ms",
            sw1.ElapsedMilliseconds, sw2.ElapsedMilliseconds);

        // Compare results
        var originalCount = fileTransfers?.Count() ?? 0;
        var denormalizedCount = fileTransfersFromDenormalized?.Count() ?? 0;

        if (originalCount != denormalizedCount)
        {
            logger.LogError("Result mismatch! Original returned {originalCount} items, Denormalized returned {denormalizedCount} items",
                originalCount, denormalizedCount);
        }
        else
        {
            // Optional: Deep comparison if counts match
            var originalIds = fileTransfers.Select(f => f.Id).OrderBy(id => id).ToList();
            var denormalizedIds = fileTransfersFromDenormalized.Select(f => f.Id).OrderBy(id => id).ToList();

            if (!originalIds.SequenceEqual(denormalizedIds))
            {
                logger.LogError("Result mismatch! Same count ({count}) but different IDs returned", originalCount);
            }
            else
            {
                logger.LogInformation("Results match - both queries returned {count} items with identical IDs", originalCount);
            }
        }

        return fileTransfers;
    }
}
