using System.Collections.Concurrent;
using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetActiveFileTransfers;

public class GetActiveFileTransfersHandler(
    IAuthorizationService authorizationService,
    IFileTransferRepository fileTransferRepository,
    IActorRepository actorRepository,
    IAltinnRegisterService altinnRegisterService,
    ILogger<GetActiveFileTransfersHandler> logger) : IHandler<GetActiveFileTransfersRequest, List<FileTransferSummaryEntity>>
{
    public async Task<OneOf<List<FileTransferSummaryEntity>, Error>> Process(GetActiveFileTransfersRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting active file transfers across {count} requested resources", request.ResourceIds.Count);

        string? caller = request.OnBehalfOf != string.Empty ? request.OnBehalfOf : user?.GetCallerOrganizationId();
        if (caller is null)
        {
            logger.LogError("Caller not found");
            return Errors.NoAccessToResource;
        }

        var callingActor = await actorRepository.GetActorAsync(caller.WithPrefix(), cancellationToken);
        if (callingActor is null)
        {
            return new List<FileTransferSummaryEntity>();
        }
        
        var authorizedResources = await authorizationService.GetAuthorizedResources(user, caller, request.ResourceIds, cancellationToken);
        var authorizedResourceIds = authorizedResources
            .Where(resource => resource.CanSend || resource.CanReceive)
            .Select(resource => resource.ResourceId)
            .ToList();

        if (authorizedResourceIds.Count == 0)
        {
            return new List<FileTransferSummaryEntity>();
        }

        var summaries = await fileTransferRepository.GetActiveFileTransferSummariesAssociatedWithActor(new ActiveFileTransferSearchEntity()
        {
            Actor = callingActor,
            ResourceIds = authorizedResourceIds,
            Status = FileTransferStatus.Published,
        }, cancellationToken);

        var uniqueOrganizationIds = summaries
            .SelectMany(summary => new[] { summary.Sender }.Concat(summary.Recipients))
            .Distinct()
            .ToList();

        var organizationNameById = new ConcurrentDictionary<string, string?>();
        await Parallel.ForEachAsync(
            uniqueOrganizationIds,
            new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = cancellationToken },
            async (organizationId, ct) =>
            {
                organizationNameById[organizationId] = await altinnRegisterService.LookupOrganizationName(organizationId, ct);
            });

        foreach (var summary in summaries)
        {
            summary.Sender = organizationNameById.GetValueOrDefault(summary.Sender) ?? summary.Sender;
            summary.Recipients = summary.Recipients
                .Select(recipient => organizationNameById.GetValueOrDefault(recipient) ?? recipient)
                .ToList();
        }

        return summaries;
    }
}
