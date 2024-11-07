using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.DownloadFile;
public class DownloadFileHandler(IResourceRepository resourceRepository, IServiceOwnerRepository serviceOwnerRepository, IAuthorizationService resourceRightsRepository, IFileTransferRepository fileTransferRepository, IActorFileTransferStatusRepository actorFileTransferStatusRepository, IBrokerStorageService brokerStorageService, ILogger<DownloadFileHandler> logger) : IHandler<DownloadFileRequest, DownloadFileResponse>
{
    public async Task<OneOf<DownloadFileResponse, Error>> Process(DownloadFileRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting download of file transfer {FileTransferId}", request.FileTransferId);
        var fileTransfer = await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        if (fileTransfer.FileTransferStatusEntity.Status != FileTransferStatus.Published && fileTransfer.FileTransferStatusEntity.Status != FileTransferStatus.AllConfirmedDownloaded)
        {
            return Errors.FileTransferNotAvailable;
        }
        if (!fileTransfer.RecipientCurrentStatuses.Any(actorEvent => actorEvent.Actor.ActorExternalId == request.Token.Consumer))
        {
            return Errors.FileTransferNotFound;
        }
        if (string.IsNullOrWhiteSpace(fileTransfer?.FileLocation))
        {
            return Errors.NoFileUploaded;
        }
        var hasAccess = await resourceRightsRepository.CheckUserAccess(fileTransfer.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        };
        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.InvalidResourceDefinition;
        };
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        };
        var downloadStream = await brokerStorageService.DownloadFile(serviceOwner, fileTransfer, cancellationToken);
        if (resource.UseManifestFileShim == true && request.IsLegacy) // For specific legacy resources during transition period
        {
            var fileBuffer = new byte[downloadStream.Length];
            downloadStream.Read(fileBuffer, 0, fileBuffer.Length);
            downloadStream = new ManifestDownloadStream(fileBuffer);
            (downloadStream as ManifestDownloadStream)?.AddManifestFile(fileTransfer, resource);
        }
        await actorFileTransferStatusRepository.InsertActorFileTransferStatus(request.FileTransferId, ActorFileTransferStatus.DownloadStarted, request.Token.Consumer, cancellationToken);
        return new DownloadFileResponse()
        {
            FileName = fileTransfer.FileName,
            DownloadStream = downloadStream
        };
    }
}
