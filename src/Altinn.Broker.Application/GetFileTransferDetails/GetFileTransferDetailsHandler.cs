using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using OneOf;

namespace Altinn.Broker.Application.GetFileTransferDetails;

public class GetFileTransferDetailsHandler : IHandler<GetFileTransferDetailsRequest, GetFileTransferDetailsResponse>
{
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IFileTransferStatusRepository _fileTransferStatusRepository;
    private readonly IActorFileTransferStatusRepository _actorFileTransferStatusRepository;

    public GetFileTransferDetailsHandler(IFileTransferRepository fileTransferRepository, IAuthorizationService resourceRightsRepository, IFileTransferStatusRepository fileTransferStatusRepository, IActorFileTransferStatusRepository actorFileTransferStatusRepository)
    {
        _fileTransferStatusRepository = fileTransferStatusRepository;
        _actorFileTransferStatusRepository = actorFileTransferStatusRepository;
        _fileTransferRepository = fileTransferRepository;
        _resourceRightsRepository = resourceRightsRepository;
    }

    public async Task<OneOf<GetFileTransferDetailsResponse, Error>> Process(GetFileTransferDetailsRequest request, CancellationToken cancellationToken)
    {
        var fileTransfer = await _fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        if (fileTransfer.Sender.ActorExternalId != request.Token.Consumer &&
            !fileTransfer.RecipientCurrentStatuses.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Token.Consumer))
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(fileTransfer.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write, ResourceAccessLevel.Read }, cancellationToken: cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var fileTransferEvents = await _fileTransferStatusRepository.GetFileTransferStatusHistory(request.FileTransferId, cancellationToken);
        var actorEvents = await _actorFileTransferStatusRepository.GetActorEvents(request.FileTransferId, cancellationToken);
        return new GetFileTransferDetailsResponse()
        {
            FileTransfer = fileTransfer,
            FileTransferEvents = fileTransferEvents,
            ActorEvents = actorEvents
        };
    }
}
