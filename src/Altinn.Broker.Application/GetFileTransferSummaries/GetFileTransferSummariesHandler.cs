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

namespace Altinn.Broker.Application.GetFileTransferSummaries;

public class GetFileTransferSummariesHandler(
    IAuthorizationService authorizationService,
    IFileTransferRepository fileTransferRepository,
    IActorRepository actorRepository,
    IAltinnRegisterService altinnRegisterService,
    ILogger<GetFileTransferSummariesHandler> logger) : IHandler<GetFileTransferSummariesRequest, FileTransferSummaryPage>
{
    /// <summary>How many file transfers a list view returns. One more is read, to tell a full page from a capped one.</summary>
    public const int PageSize = 100;

    private static readonly List<FileTransferStatus> ActiveSenderStatuses = [FileTransferStatus.UploadProcessing, FileTransferStatus.Published];
    private static readonly List<FileTransferStatus> ActiveRecipientStatuses = [FileTransferStatus.Published];

    /// <summary>
    /// A file transfer is historical once it has moved past Published into any of these terminal
    /// states - mirrors the terminal-state set used to derive the active-detail page's Status field.
    /// </summary>
    private static readonly List<FileTransferStatus> HistoricalStatuses =
    [
        FileTransferStatus.Cancelled,
        FileTransferStatus.AllConfirmedDownloaded,
        FileTransferStatus.Purged,
        FileTransferStatus.Failed,
    ];

    public async Task<OneOf<FileTransferSummaryPage, Error>> Process(GetFileTransferSummariesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting {view} file transfers across {count} requested resources", request.View, request.ResourceIds.Count);

        string? caller = request.OnBehalfOf != string.Empty ? request.OnBehalfOf : user?.GetCallerOrganizationId();
        if (caller is null)
        {
            logger.LogError("Caller not found");
            return Errors.NoAccessToResource;
        }

        var callingActor = await actorRepository.GetActorAsync(caller.WithPrefix(), cancellationToken);
        if (callingActor is null)
        {
            return FileTransferSummaryPage.Empty();
        }

        var authorizedResources = await authorizationService.GetAuthorizedResources(user, caller, request.ResourceIds, cancellationToken);
        var authorizedResourceIds = authorizedResources
            .Where(resource => resource.CanSend || resource.CanReceive)
            .Select(resource => resource.ResourceId)
            .ToList();

        if (authorizedResourceIds.Count == 0)
        {
            return FileTransferSummaryPage.Empty();
        }

        var senderStatuses = request.View == FileTransferListView.Active ? ActiveSenderStatuses : HistoricalStatuses;
        var recipientStatuses = request.View == FileTransferListView.Active ? ActiveRecipientStatuses : HistoricalStatuses;
        // One past the page tells a capped list from a complete one, without counting the whole set.
        var summaries = await fileTransferRepository.GetFileTransferSummariesAssociatedWithActor(new FrontendFileTransferSearchEntity()
        {
            Actor = callingActor,
            ResourceIds = authorizedResourceIds,
            SenderStatuses = senderStatuses,
            RecipientStatuses = recipientStatuses,
            From = request.From,
            To = request.To,
            Limit = PageSize + 1,
        }, cancellationToken);

        var hasMore = summaries.Count > PageSize;
        if (hasMore)
        {
            summaries = summaries.Take(PageSize).ToList();
        }

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

        return new FileTransferSummaryPage { Summaries = summaries, HasMore = hasMore };
    }
}
