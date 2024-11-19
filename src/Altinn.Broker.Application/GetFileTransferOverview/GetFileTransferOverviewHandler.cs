using System.Security.Claims;

using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileTransferOverview;

public class GetFileTransferOverviewHandler(IAuthorizationService resourceRightsRepository, IFileTransferRepository fileTransferRepository, ILogger<GetFileTransferOverviewHandler> logger) : IHandler<GetFileTransferOverviewRequest, GetFileTransferOverviewResponse>
{
    public async Task<OneOf<GetFileTransferOverviewResponse, Error>> Process(GetFileTransferOverviewRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrieving file overview for file transfer {fileTransferId}. Legacy: {legacy}", request.FileTransferId, request.IsLegacy);
        var fileTransfer = await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        if (fileTransfer.Sender.ActorExternalId != request.Token.Consumer &&
            !fileTransfer.RecipientCurrentStatuses.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Token.Consumer))
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await resourceRightsRepository.CheckUserAccess(user, fileTransfer.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write, ResourceAccessLevel.Read }, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        return new GetFileTransferOverviewResponse()
        {
            FileTransfer = fileTransfer
        };
    }
}
