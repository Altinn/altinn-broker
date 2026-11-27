using System.Security.Claims;

using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using OneOf;

namespace Altinn.Broker.Application.UploadPlans;

/// <summary>
/// An upload plan is read and written by whoever may upload to the transfer: the same sender check as
/// the tus endpoints, while the transfer still takes uploads.
/// </summary>
public class UploadPlanAccess(
    IAuthorizationService authorizationService,
    IFileTransferRepository fileTransferRepository)
{
    public async Task<OneOf<FileTransferEntity, Error>> GetUploadableFileTransfer(
        Guid fileTransferId,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
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

        if (fileTransfer.FileTransferStatusEntity.Status > FileTransferStatus.UploadStarted)
        {
            return Errors.FileTransferAlreadyUploaded;
        }

        return fileTransfer;
    }
}
