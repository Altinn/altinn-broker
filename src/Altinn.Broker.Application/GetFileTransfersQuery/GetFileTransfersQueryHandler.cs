using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileTransfersQuery;

public class GetFileTransfersQueryHandler : IHandler<GetFileTransfersQueryRequest, List<Guid>>
{
    private readonly IAuthorizationService _resourceRightsRepository;
    private readonly IResourceRepository _resourceRepository;
    private readonly IFileTransferRepository _fileTransferRepository;
    private readonly IActorRepository _actorRepository;
    private readonly ILogger<GetFileTransfersQueryHandler> _logger;

    public GetFileTransfersQueryHandler(IAuthorizationService resourceRightsRepository, IResourceRepository resourceRepository, IFileTransferRepository fileTransferRepository, IActorRepository actorRepository, ILogger<GetFileTransfersQueryHandler> logger)
    {
        _resourceRightsRepository = resourceRightsRepository;
        _resourceRepository = resourceRepository;
        _fileTransferRepository = fileTransferRepository;
        _actorRepository = actorRepository;
        _logger = logger;
    }

    public async Task<OneOf<List<Guid>, Error>> Process(GetFileTransfersQueryRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _resourceRightsRepository.CheckUserAccess(request.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write, ResourceAccessLevel.Read }, cancellationToken: cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var service = await _resourceRepository.GetResource(request.ResourceId, cancellationToken);
        if (service is null)
        {
            return Errors.ResourceNotConfigured;
        };
        var callingActor = await _actorRepository.GetActorAsync(request.Token.Consumer, cancellationToken);
        if (callingActor is null)
        {
            return new List<Guid>();
        }

        FileTransferSearchEntity fileTransferSearchEntity = new()
        {
            Actor = callingActor,
            ResourceId = request.ResourceId,
            Status = request.Status
        };

        if (request.From.HasValue)
        {
            fileTransferSearchEntity.From = new DateTimeOffset(request.From.Value.UtcDateTime, TimeSpan.Zero);
        }

        if (request.To.HasValue)
        {
            fileTransferSearchEntity.To = new DateTimeOffset(request.To.Value.UtcDateTime, TimeSpan.Zero);
        }

        if (request.RecipientStatus.HasValue)
        {
            fileTransferSearchEntity.RecipientStatus = request.RecipientStatus;
            return await _fileTransferRepository.GetFileTransfersForRecipientWithRecipientStatus(fileTransferSearchEntity, cancellationToken);
        }
        else
        {
            return await _fileTransferRepository.GetFileTransfersAssociatedWithActor(fileTransferSearchEntity, cancellationToken);
        }
    }
}
