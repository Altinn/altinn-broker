using System.Security.Claims;

using Altinn.Broker.Application.UploadFile.Tus;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetPartialUploads;

public class GetPartialUploadsHandler(
    IAuthorizationService authorizationService,
    IFileTransferRepository fileTransferRepository,
    ITusPartialUploadLookup partialUploadLookup,
    ILogger<GetPartialUploadsHandler> logger) : IHandler<GetPartialUploadsRequest, GetPartialUploadsResponse>
{
    public async Task<OneOf<GetPartialUploadsResponse, Error>> Process(GetPartialUploadsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrieving partial uploads for file transfer {fileTransferId}", request.FileTransferId);

        var fileTransfer = await TransactionWithRetriesPolicy.Execute(
            async (cancellationToken) => await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken),
            logger,
            cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }

        var hasAccess = await authorizationService.CheckAccessAsSender(
            user,
            fileTransfer.ResourceId,
            fileTransfer.Sender.ActorExternalId,
            cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        return new GetPartialUploadsResponse
        {
            PartialUploads = await partialUploadLookup.GetPartialUploadsAsync(request.FileTransferId, cancellationToken)
        };
    }
}
