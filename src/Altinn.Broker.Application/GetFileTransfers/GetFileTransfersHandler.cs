using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileTransfers;

public class GetFileTransfersHandler(IAuthorizationService resourceRightsRepository, IResourceRepository resourceRepository, IFileTransferRepository fileTransferRepository, IActorRepository actorRepository, ILogger<GetFileTransfersHandler> logger) : IHandler<GetFileTransfersRequest, List<Guid>>
{
    public async Task<OneOf<List<Guid>, Error>> Process(GetFileTransfersRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await resourceRightsRepository.CheckUserAccess(request.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write, ResourceAccessLevel.Read }, cancellationToken: cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var service = await resourceRepository.GetResource(request.ResourceId, cancellationToken);
        if (service is null)
        {
            return Errors.InvalidResourceDefinition;
        };
        var callingActor = await actorRepository.GetActorAsync(request.Token.Consumer, cancellationToken);
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
            return await fileTransferRepository.GetFileTransfersForRecipientWithRecipientStatus(fileTransferSearchEntity, cancellationToken);
        }
        else
        {
            return await fileTransferRepository.GetFileTransfersAssociatedWithActor(fileTransferSearchEntity, cancellationToken);
        }
    }
}
