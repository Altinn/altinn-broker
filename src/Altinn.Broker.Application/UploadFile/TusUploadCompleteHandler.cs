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
            logger.LogWarning("TUS completion aborted: file transfer {FileTransferId} not found", fileTransferId);
            return Errors.FileTransferNotFound;
        }

        logger.LogInformation(
            "TUS completion starting for file transfer {FileTransferId}. CurrentStatus={CurrentStatus}",
            fileTransferId,
            fileTransfer.FileTransferStatusEntity.Status);

        var resource = await resourceRepository.GetResource(fileTransfer.ResourceId, cancellationToken);
        if (resource is null)
        {
            logger.LogWarning(
                "TUS completion aborted: resource {ResourceId} not found for file transfer {FileTransferId}",
                fileTransfer.ResourceId,
                fileTransferId);
            return Errors.InvalidResourceDefinition;
        }

        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId);
        if (serviceOwner is null)
        {
            logger.LogWarning(
                "TUS completion aborted: service owner {ServiceOwnerId} not configured for file transfer {FileTransferId}",
                resource.ServiceOwnerId,
                fileTransferId);
            return Errors.ServiceOwnerNotConfigured;
        }

        if (fileTransfer.FileTransferStatusEntity.Status is not (
            FileTransferStatus.UploadStarted or FileTransferStatus.UploadProcessing))
        {
            logger.LogInformation(
                "TUS completion skipped for file transfer {FileTransferId}: status already {CurrentStatus}",
                fileTransferId,
                fileTransfer.FileTransferStatusEntity.Status);
            return fileTransferId;
        }

        var finalizeResult = await brokerStorageService.FinalizeTusUpload(serviceOwner, fileTransfer, cancellationToken);
        if (finalizeResult is null)
        {
            logger.LogError(
                "TUS completion failed for file transfer {FileTransferId}: FinalizeTusUpload returned null",
                fileTransferId);
            return Errors.UploadFailed;
        }

        var (checksum, uploadLength) = finalizeResult.Value;
        logger.LogInformation(
            "TUS storage finalized for file transfer {FileTransferId}. UploadLength={UploadLength} ChecksumPresent={ChecksumPresent}",
            fileTransferId,
            uploadLength,
            !string.IsNullOrWhiteSpace(checksum));

        var completeResult = await completeFileUploadHandler.Process(
            new CompleteFileUploadRequest
            {
                FileTransferId = fileTransferId,
                Checksum = checksum,
                UploadLength = uploadLength,
                DeferChecksumValidation = true
            },
            user,
            cancellationToken);

        if (completeResult.IsT1)
        {
            logger.LogError(
                "TUS CompleteFileUploadHandler failed for file transfer {FileTransferId}: {ErrorMessage}",
                fileTransferId,
                completeResult.AsT1.Message);
        }
        else
        {
            logger.LogInformation(
                "TUS CompleteFileUploadHandler succeeded for file transfer {FileTransferId}",
                fileTransferId);
        }

        return completeResult;
    }
}
