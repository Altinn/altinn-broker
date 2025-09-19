using System.Security.Claims;

using Altinn.Broker.Core;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileTransferOverview;

public class GetFileTransferOverviewHandler(IAuthorizationService authorizationService, IFileTransferRepository fileTransferRepository, ILogger<GetFileTransferOverviewHandler> logger) : IHandler<GetFileTransferOverviewRequest, GetFileTransferOverviewResponse>
{
    public async Task<OneOf<GetFileTransferOverviewResponse, Error>> Process(GetFileTransferOverviewRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrieving file overview for file transfer {fileTransferId}. Legacy: {legacy}", request.FileTransferId, request.IsLegacy);

        var fileTransfer = await TransactionWithRetriesPolicy.Execute(
            async (cancellationToken) => await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken),
            logger,
            cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await authorizationService.CheckAccessAsSenderOrRecipient(user, fileTransfer, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        if (request.IsLegacy && request.OnBehalfOfConsumer is not null && !fileTransfer.IsSenderOrRecipient(request.OnBehalfOfConsumer))
        {
            return Errors.NoAccessToResource;
        }
        return new GetFileTransferOverviewResponse()
        {
            FileTransfer = fileTransfer
        };
    }

    public async Task<OneOf<GetFileTransferOverviewsResponse, Error>> ProcessMultiple(GetFileTransferOverviewRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var ids = request.FileTransferIds ?? [];
        logger.LogInformation("Retrieving file overview for {Transfers} file transfers. Legacy: {Legacy}", ids.Count, request.IsLegacy);
        var fileTransfers = await TransactionWithRetriesPolicy.Execute(
        async (cancellationToken) => await fileTransferRepository.GetFileTransfers(ids, cancellationToken),
             logger,
             cancellationToken);
        if (fileTransfers is null)
        {
            return Errors.FileTransferNotFound;
        }
        var accessChecks = await Task.WhenAll(
        fileTransfers.Select(ft => authorizationService.CheckAccessAsSenderOrRecipient(user, ft, request.IsLegacy, cancellationToken)));
        if (accessChecks.Any(allowed => !allowed))
        {
            return Errors.NoAccessToResource;
        }
        if (request.IsLegacy && !string.IsNullOrWhiteSpace(request.OnBehalfOfConsumer)
            && fileTransfers.Any(ft => !ft.IsSenderOrRecipient(request.OnBehalfOfConsumer!)))
        {
            return Errors.NoAccessToResource;
        }
        return new GetFileTransferOverviewsResponse()
        {
            FileTransfers = fileTransfers
        };
    }
}
