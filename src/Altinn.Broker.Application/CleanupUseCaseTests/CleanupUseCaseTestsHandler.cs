using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Hangfire;
using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Repositories;
using OneOf;
using Altinn.Broker.Core.Helpers;


namespace Altinn.Broker.Application.CleanupUseCaseTests;

public class CleanupUseCaseTestsHandler(
    IBackgroundJobClient backgroundJobClient,
    ILogger<CleanupUseCaseTestsHandler> logger,
    IFileTransferRepository fileTransferRepository,
    IResourceRepository resourceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IBrokerStorageService brokerStorageService
) : IHandler<CleanupUseCaseTestsRequest, CleanupUseCaseTestsResponse>
{
    private const string ResourceId = "bruksmonster-broker";

    public async Task<OneOf<CleanupUseCaseTestsResponse, Error>> Process(CleanupUseCaseTestsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting cleanup of use case test data");
        var minAge = DateTimeOffset.UtcNow;
        if (request.MinAgeDays.HasValue)
        {
            minAge = minAge.Subtract(TimeSpan.FromDays(request.MinAgeDays.Value));
        }

        var fileTransferIds = await fileTransferRepository.GetFileTransfersByResourceId(ResourceId, minAge, cancellationToken);

        return await TransactionWithRetriesPolicy.Execute(async (ct) =>
        {
            var deleteFileTransfersJobId = backgroundJobClient.Enqueue<CleanupUseCaseTestsHandler>(h => h.DeleteFileTransfers(fileTransferIds, ResourceId, minAge, CancellationToken.None));
            await Task.CompletedTask;

            var response = new CleanupUseCaseTestsResponse
            {
                ResourceId = ResourceId,
                FileTransfersFound = fileTransferIds.Count,
                DeleteFileTransfersJobId = deleteFileTransfersJobId
            };
            return response;
        }, logger, cancellationToken);
    }


	public async Task DeleteFileTransfers(List<Guid> fileTransferIds, string resourceId, DateTimeOffset minAge, CancellationToken cancellationToken)
    {
        var blobDeletionFailures = await DeleteRemainingUseCaseBlobs(resourceId, minAge, cancellationToken);

        var fileTransferIdsToDelete = blobDeletionFailures.Count == 0
            ? fileTransferIds
            : fileTransferIds.Where(id => !blobDeletionFailures.Contains(id)).ToList();

        await TransactionWithRetriesPolicy.Execute(async (ct) =>
        {
            int deletedFileTransfers = await fileTransferRepository.HardDeleteFileTransfersByIds(fileTransferIdsToDelete, cancellationToken);
            logger.LogInformation("Deleted {deletedFileTransfers} file transfers for resourceId {resourceId} ({keptForRetry} kept for retry due to blob deletion failures)", deletedFileTransfers, resourceId, blobDeletionFailures.Count);

            return Task.CompletedTask;
        }, logger, cancellationToken);
    }

    private async Task<HashSet<Guid>> DeleteRemainingUseCaseBlobs(string resourceId, DateTimeOffset minAge, CancellationToken cancellationToken)
    {
        var fileTransfersToDelete = await fileTransferRepository.GetNonPurgedFileTransfersByResourceId(resourceId, minAge, cancellationToken);
        if (fileTransfersToDelete.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var resource = await resourceRepository.GetResource(resourceId, cancellationToken)
            ?? throw new InvalidOperationException($"Resource {resourceId} not found; aborting use case test cleanup to avoid orphaning blobs");
        var serviceOwner = await serviceOwnerRepository.GetServiceOwner(resource.ServiceOwnerId)
            ?? throw new InvalidOperationException($"Service owner {resource.ServiceOwnerId} not found; aborting use case test cleanup to avoid orphaning blobs");

        HashSet<Guid> failedFileTransferIds = [];
        foreach (var fileTransfer in fileTransfersToDelete)
        {
            try
            {
                await brokerStorageService.DeleteFile(serviceOwner, fileTransfer, cancellationToken);
            }
            catch (Exception ex)
            {
                failedFileTransferIds.Add(fileTransfer.FileTransferId);
                logger.LogError(ex, "Failed to delete blob for file transfer {fileTransferId} during use case test cleanup", fileTransfer.FileTransferId);
            }
        }
        return failedFileTransferIds;
    }
}
