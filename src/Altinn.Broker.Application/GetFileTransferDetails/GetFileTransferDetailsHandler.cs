using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileTransferDetails;

public class GetFileTransferDetailsHandler(IFileTransferRepository fileTransferRepository, IAuthorizationService resourceRightsRepository, IFileTransferStatusRepository fileTransferStatusRepository, IActorFileTransferStatusRepository actorFileTransferStatusRepository, ILogger<GetFileTransferDetailsHandler> logger) : IHandler<GetFileTransferDetailsRequest, GetFileTransferDetailsResponse>
{
    public async Task<OneOf<GetFileTransferDetailsResponse, Error>> Process(GetFileTransferDetailsRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting file transfer details for {fileTransferId}.", request.FileTransferId);
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
        var hasAccess = await resourceRightsRepository.CheckUserAccess(fileTransfer.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write, ResourceAccessLevel.Read }, cancellationToken: cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var fileTransferEvents = await fileTransferStatusRepository.GetFileTransferStatusHistory(request.FileTransferId, cancellationToken);
        var actorEvents = await actorFileTransferStatusRepository.GetActorEvents(request.FileTransferId, cancellationToken);
        return new GetFileTransferDetailsResponse()
        {
            FileTransfer = fileTransfer,
            FileTransferEvents = fileTransferEvents,
            ActorEvents = actorEvents
        };
    }
}
