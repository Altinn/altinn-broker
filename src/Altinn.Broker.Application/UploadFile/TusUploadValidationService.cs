using System.Security.Claims;

using Altinn.Broker.Application.Settings;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

namespace Altinn.Broker.Application.UploadFile;

public class TusUploadValidationService(
    IAuthorizationService authorizationService,
    IFileTransferRepository fileTransferRepository,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository)
{
    public async Task<(FileTransferEntity FileTransfer, long? MaxUploadSize, Error? Error)> ValidateForUploadAsync(
        ClaimsPrincipal? user,
        Guid fileTransferId,
        long? uploadLength,
        bool isLegacyUser = false,
        CancellationToken cancellationToken = default)
    {
        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return (null!, null, Errors.FileTransferNotFound);
        }

        var hasAccess = await authorizationService.CheckAccessAsSender(
            user,
            fileTransfer.ResourceId,
            fileTransfer.Sender.ActorExternalId,
            isLegacyUser,
            cancellationToken);
        if (!hasAccess)
        {
            return (fileTransfer, null, Errors.NoAccessToResource);
        }

        if (fileTransfer.FileTransferStatusEntity.Status > FileTransferStatus.UploadStarted)
        {
            return (fileTransfer, null, Errors.FileTransferAlreadyUploaded);
        }

        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            return (fileTransfer, null, Errors.InvalidResourceDefinition);
        }

        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return (fileTransfer, null, Errors.ServiceOwnerNotConfigured);
        }

        var storageProvider = serviceOwner.GetStorageProvider(fileTransfer.UseVirusScan);
        if (storageProvider is null)
        {
            return (fileTransfer, null, Errors.StorageProviderNotReady);
        }

        if (uploadLength is not null)
        {
            if (fileTransfer.UseVirusScan && uploadLength > ApplicationConstants.MaxVirusScanUploadSize)
            {
                return (fileTransfer, null, Errors.FileSizeTooBig);
            }

            if (resource.MaxFileTransferSize is not null && uploadLength > resource.MaxFileTransferSize)
            {
                return (fileTransfer, null, Errors.FileSizeTooBig);
            }
        }

        return (fileTransfer, resource.MaxFileTransferSize, null);
    }
}
