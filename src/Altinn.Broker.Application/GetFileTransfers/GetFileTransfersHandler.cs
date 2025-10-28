using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileTransfers;

public class GetFileTransfersHandler(IAuthorizationService authorizationService, IResourceRepository resourceRepository, IFileTransferRepository fileTransferRepository, IActorRepository actorRepository, ILogger<GetFileTransfersHandler> logger) : IHandler<GetFileTransfersRequest, List<Guid>>
{
    public async Task<OneOf<List<Guid>, Error>> Process(GetFileTransfersRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting file transfers for {resourceId}", request.ResourceId.SanitizeForLogs());
        var caller = user?.GetCallerOrganizationId();
        if (caller is null)
        {
            logger.LogError("Caller not found");
            return Errors.NoAccessToResource;
        }
        var hasAccess = await authorizationService.CheckAccessForSearch(user, request.ResourceId, caller, false, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var service = await resourceRepository.GetResource(request.ResourceId, cancellationToken);
        if (service is null)
        {
            return Errors.InvalidResourceDefinition;
        };
        var callingActor = await actorRepository.GetActorAsync(caller.WithPrefix(), cancellationToken);
        if (callingActor is null)
        {
            return new List<Guid>();
        }

        FileTransferSearchEntity fileTransferSearchEntity = new()
        {
            Actor = callingActor,
            ResourceId = request.ResourceId,
            Status = request.Status,
            Role = request.Role
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
