using System.Security.Claims;

using Altinn.Broker.Application.Settings;
using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Options;

namespace Altinn.Broker.Application.UploadFile.Tus;

public class TusUploadValidationService(
    IAuthorizationService authorizationService,
    IFileTransferRepository fileTransferRepository,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IOptions<AzureStorageOptions> azureStorageOptions)
{
    public async Task<(FileTransferEntity FileTransfer, long? MaxUploadSize, Error? Error)> ValidateForUploadAsync(
        ClaimsPrincipal? user,
        Guid fileTransferId,
        long? uploadLength,
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

    public async Task<Error?> ValidateTusGetInfoAsync(
        ClaimsPrincipal? user,
        Guid fileTransferId,
        CancellationToken cancellationToken = default)
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

        return null;
    }

    public async Task<Error?> ValidateUploadInProgressAsync(Guid fileTransferId, CancellationToken cancellationToken)
    {
        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }

        if (!IsTusUploadSessionStatus(fileTransfer.FileTransferStatusEntity.Status))
        {
            return Errors.FileTransferAlreadyUploaded;
        }

        return null;
    }

    private static bool IsTusUploadSessionStatus(FileTransferStatus status)
        => status is FileTransferStatus.UploadStarted or FileTransferStatus.UploadProcessing;

    /// <summary>
    /// Lightweight sender check for in-progress uploads. Used when the upload session is already
    /// established so standard TUS clients can resume HEAD/PATCH without repeating full resource authorization.
    /// </summary>
    public async Task<Error?> ValidateActiveUploadSenderAsync(
        ClaimsPrincipal? user,
        Guid fileTransferId,
        CancellationToken cancellationToken = default)
    {
        var inProgressError = await ValidateUploadInProgressAsync(fileTransferId, cancellationToken);
        if (inProgressError is not null)
        {
            return inProgressError;
        }

        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }

        var callerOrganization = user?.GetCallerOrganizationId();
        if (string.IsNullOrEmpty(callerOrganization)
            || !string.Equals(
                fileTransfer.Sender.ActorExternalId.WithoutPrefix(),
                callerOrganization.WithoutPrefix(),
                StringComparison.Ordinal))
        {
            return Errors.NoAccessToResource;
        }

        return null;
    }

    /// <param name="singleUploadLength">
    /// The length this one TUS upload declares, as opposed to the transfer total. Null when no new
    /// upload is being created, as on a concatenation final request.
    /// </param>
    public async Task<(long? MaxUploadSize, Error? Error)> ValidateUploadSizeAsync(
        Guid fileTransferId,
        long uploadLength,
        long? singleUploadLength,
        CancellationToken cancellationToken)
    {
        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return (null, Errors.FileTransferNotFound);
        }

        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            return (null, Errors.InvalidResourceDefinition);
        }

        if (fileTransfer.UseVirusScan && uploadLength > ApplicationConstants.MaxVirusScanUploadSize)
        {
            return (resource.MaxFileTransferSize, Errors.FileSizeTooBig);
        }

        if (resource.MaxFileTransferSize is not null && uploadLength > resource.MaxFileTransferSize)
        {
            return (resource.MaxFileTransferSize, Errors.FileSizeTooBig);
        }

        if (singleUploadLength > StripeLayout.MaxPartialLength(
                fileTransfer.StripeSizeBytes ?? 0,
                azureStorageOptions.Value.MaxBlocksPerStripe))
        {
            return (resource.MaxFileTransferSize, Errors.PartialUploadTooLong);
        }

        return (resource.MaxFileTransferSize, null);
    }
}
