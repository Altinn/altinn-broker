using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileTransferOverview;

public class GetFileTransferOverviewHandler : IHandler<GetFileTransferOverviewRequest, GetFileTransferOverviewResponse>
{
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly ILogger<GetFileTransferOverviewHandler> _logger;

    public GetFileTransferOverviewHandler(IAuthorizationService resourceRightsRepository, IFileTransferRepository fileTransferRepository, IResourceManager resourceManager, ILogger<GetFileTransferOverviewHandler> logger)
    {
        _resourceRightsRepository = resourceRightsRepository;
        _fileTransferRepository = fileTransferRepository;
        _logger = logger;
    }

    public async Task<OneOf<GetFileTransferOverviewResponse, Error>> Process(GetFileTransferOverviewRequest request, CancellationToken cancellationToken)
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
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(fileTransfer.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write, ResourceAccessLevel.Read }, request.IsLegacy, cancellationToken);
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
