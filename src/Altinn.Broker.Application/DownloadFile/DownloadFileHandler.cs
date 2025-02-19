using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.DownloadFile;
public class DownloadFileHandler(IResourceRepository resourceRepository, IServiceOwnerRepository serviceOwnerRepository, IAuthorizationService authorizationService, IFileTransferRepository fileTransferRepository, IActorFileTransferStatusRepository actorFileTransferStatusRepository, IBrokerStorageService brokerStorageService, ILogger<DownloadFileHandler> logger) : IHandler<DownloadFileRequest, DownloadFileResponse>
{
    public async Task<OneOf<DownloadFileResponse, Error>> Process(DownloadFileRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting download of file transfer {FileTransferId}", request.FileTransferId);
        var fileTransfer = await fileTransferRepository.GetFileTransfer(request.FileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }
        var hasAccess = await authorizationService.CheckAccessAsRecipient(user, fileTransfer, request.IsLegacy, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        ;
        if (request.IsLegacy && request.OnBehalfOfConsumer is not null && !fileTransfer.IsRecipient(request.OnBehalfOfConsumer))
        {
            return Errors.NoAccessToResource;
        }
        if (fileTransfer.FileTransferStatusEntity.Status != FileTransferStatus.Published && fileTransfer.FileTransferStatusEntity.Status != FileTransferStatus.AllConfirmedDownloaded)
        {
            return Errors.FileTransferNotAvailable;
        }
        if (string.IsNullOrWhiteSpace(fileTransfer?.FileLocation))
        {
            return Errors.NoFileUploaded;
        }
        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.InvalidResourceDefinition;
        }
        ;
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        }
        ;
        var downloadStream = await brokerStorageService.DownloadFile(serviceOwner, fileTransfer, cancellationToken);
        if (resource.UseManifestFileShim == true && request.IsLegacy) // For specific legacy resources during transition period
        {
            var fileBuffer = new byte[fileTransfer.FileTransferSize];
            downloadStream.ReadExactly(fileBuffer, 0, fileBuffer.Length);
            downloadStream = new ManifestDownloadStream(fileBuffer);
            (downloadStream as ManifestDownloadStream)?.AddManifestFile(fileTransfer, resource);
        }
        var caller = request.OnBehalfOfConsumer ?? user?.GetCallerOrganizationId();
        await actorFileTransferStatusRepository.InsertActorFileTransferStatus(request.FileTransferId, ActorFileTransferStatus.DownloadStarted, caller.WithPrefix(), cancellationToken);
        return new DownloadFileResponse()
        {
            FileName = fileTransfer.FileName,
            DownloadStream = downloadStream
        };
    }
}
