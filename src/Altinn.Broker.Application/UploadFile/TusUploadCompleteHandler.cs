using System.Security.Claims;

using Altinn.Broker.Common;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.UploadFile;

public class TusUploadCompleteHandler(
    IFileTransferRepository fileTransferRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IResourceRepository resourceRepository,
    IBrokerStorageService brokerStorageService,
    CompleteFileUploadHandler completeFileUploadHandler,
    ILogger<TusUploadCompleteHandler> logger)
{
    public async Task<OneOf<Guid, Error>> Process(Guid fileTransferId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Completing TUS upload for file transfer {fileTransferId}", fileTransferId);

        var fileTransfer = await fileTransferRepository.GetFileTransfer(fileTransferId, cancellationToken);
        if (fileTransfer is null)
        {
            return Errors.FileTransferNotFound;
        }

        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Errors.InvalidResourceDefinition;
        }

        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            return Errors.ServiceOwnerNotConfigured;
        }

        if (fileTransfer.FileTransferStatusEntity.Status > FileTransferStatus.UploadStarted)
        {
            return fileTransferId;
        }

        var finalizeResult = await brokerStorageService.FinalizeTusUpload(serviceOwner, fileTransfer, cancellationToken);
        if (finalizeResult is null)
        {
            return Errors.UploadFailed;
        }

        var (checksum, uploadLength) = finalizeResult.Value;
        return await completeFileUploadHandler.Process(
            new CompleteFileUploadRequest
            {
                FileTransferId = fileTransferId,
                Checksum = checksum,
                UploadLength = uploadLength,
                DeferChecksumValidation = true
            },
            user,
            cancellationToken);
    }
}
