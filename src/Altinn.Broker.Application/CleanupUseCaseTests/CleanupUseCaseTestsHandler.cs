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
    IFileTransferRepository fileTransferRepository
) : IHandler<CleanupUseCaseTestsResponse>
{
    public async Task<OneOf<CleanupUseCaseTestsResponse, Error>> Process(ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting cleanup of use case test data");
        var resourceId = "bruksmonster-broker";
        var fileTransferIds = await fileTransferRepository.GetFileTransfersByResourceId(resourceId, cancellationToken);
		return await TransactionWithRetriesPolicy.Execute<CleanupUseCaseTestsResponse>(async (ct) =>
        {
            
            var deleteFileTransfersJobId = backgroundJobClient.Enqueue<CleanupUseCaseTestsHandler>(h => h.DeleteFileTransfers(fileTransferIds, resourceId, CancellationToken.None));
            await Task.CompletedTask;

            var response = new CleanupUseCaseTestsResponse
            {
                ResourceId = resourceId,
                FileTransfersFound = fileTransferIds.Count,
                DeleteFileTransfersJobId = deleteFileTransfersJobId
            };
            return response;
        }, logger, cancellationToken);
    }


	public async Task DeleteFileTransfers(List<Guid> correspondenceIds, string resourceId, CancellationToken cancellationToken)
    {
        await TransactionWithRetriesPolicy.Execute<Task>(async (ct) =>
        {
            int deletedFileTransfersawait = await fileTransferRepository.HardDeleteFileTransfersByIds(correspondenceIds, cancellationToken);
            logger.LogInformation("Deleted {deletedFileTransfers} file transfers for resourceId {resourceId}", deletedFileTransfersawait, resourceId);
			
            return Task.CompletedTask;
        }, logger, cancellationToken);
    }
}

