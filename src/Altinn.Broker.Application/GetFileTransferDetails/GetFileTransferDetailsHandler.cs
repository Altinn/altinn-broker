using System.Security.Claims;

using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileTransferDetails;

public class GetFileTransferDetailsHandler(IFileTransferRepository fileTransferRepository, IAuthorizationService authorizationService, IFileTransferStatusRepository fileTransferStatusRepository, IActorFileTransferStatusRepository actorFileTransferStatusRepository, ILogger<GetFileTransferDetailsHandler> logger) : IHandler<GetFileTransferDetailsRequest, GetFileTransferDetailsResponse>
{
    public async Task<OneOf<GetFileTransferDetailsResponse, Error>> Process(GetFileTransferDetailsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting file transfer details for {fileTransferId}.", request.FileTransferId);
        var fileTransfer = await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await authorizationService.CheckAccessAsSenderOrRecipient(user, fileTransfer, false, cancellationToken);
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
