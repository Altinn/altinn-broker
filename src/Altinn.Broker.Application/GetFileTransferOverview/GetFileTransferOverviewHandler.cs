using System.Security.Claims;

using Altinn.Broker.Core;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetFileTransferOverview;

public class GetFileTransferOverviewHandler(IAuthorizationService authorizationService, IFileTransferRepository fileTransferRepository, IFileTransferStatusRepository fileTransferStatusRepository,
 ILogger<GetFileTransferOverviewHandler> logger) : IHandler<GetFileTransferOverviewRequest, GetFileTransferOverviewResponse>
{
    public async Task<OneOf<GetFileTransferOverviewResponse, Error>> Process(GetFileTransferOverviewRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrieving file overview for file transfer {fileTransferId}", request.FileTransferId);

        var fileTransfer = await TransactionWithRetriesPolicy.Execute(
            async (cancellationToken) => await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken),
            logger,
            cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await authorizationService.CheckAccessAsSenderOrRecipient(user, fileTransfer, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var fileTransferEvents = await fileTransferStatusRepository.GetFileTransferStatusHistory(request.FileTransferId, cancellationToken);
        return new GetFileTransferOverviewResponse()
        {
            FileTransfer = fileTransfer,
            FileTransferEvents = fileTransferEvents
        };
    }
}
