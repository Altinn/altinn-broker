using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileTransferOverviewQuery;

public class GetFileTransferOverviewQueryHandler : IHandler<GetFileTransferOverviewQueryRequest, GetFileTransferOverviewQueryResponse>
{
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly ILogger<GetFileTransferOverviewQueryHandler> _logger;

    public GetFileTransferOverviewQueryHandler(IAuthorizationService resourceRightsRepository, IFileTransferRepository fileTransferRepository, IResourceManager resourceManager, ILogger<GetFileTransferOverviewQueryHandler> logger)
    {
        _resourceRightsRepository = resourceRightsRepository;
        _fileTransferRepository = fileTransferRepository;
        _logger = logger;
    }

    public async Task<OneOf<GetFileTransferOverviewQueryResponse, Error>> Process(GetFileTransferOverviewQueryRequest request, CancellationToken cancellationToken)
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
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(fileTransfer.ResourceId, request.Token.ClientId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write, ResourceAccessLevel.Read }, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        return new GetFileTransferOverviewQueryResponse()
        {
            FileTransfer = fileTransfer
        };
    }
}
