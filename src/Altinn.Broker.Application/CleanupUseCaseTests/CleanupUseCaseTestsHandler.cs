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
) : IHandler<CleanupUseCaseTestsRequest, CleanupUseCaseTestsResponse>
{
    private const string ResourceId = "bruksmonster-broker";
    private const string TestTagPropertyKey = "testTag";

    public async Task<OneOf<CleanupUseCaseTestsResponse, Error>> Process(CleanupUseCaseTestsRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting cleanup of use case test data for testTag: {TestTag}", request.TestTag);
        var fileTransferIds = await fileTransferRepository.GetFileTransfersByPropertyTag(ResourceId, TestTagPropertyKey, request.TestTag, cancellationToken);
        return await TransactionWithRetriesPolicy.Execute<CleanupUseCaseTestsResponse>(async (ct) =>
        {
            var deleteFileTransfersJobId = backgroundJobClient.Enqueue<CleanupUseCaseTestsHandler>(h => h.DeleteFileTransfers(fileTransferIds, ResourceId, request.TestTag, CancellationToken.None));
            await Task.CompletedTask;

            var response = new CleanupUseCaseTestsResponse
            {
                ResourceId = ResourceId,
                TestTag = request.TestTag,
                FileTransfersFound = fileTransferIds.Count,
                DeleteFileTransfersJobId = deleteFileTransfersJobId
            };
            return response;
        }, logger, cancellationToken);
    }


	public async Task DeleteFileTransfers(List<Guid> fileTransferIds, string resourceId, string testTag, CancellationToken cancellationToken)
    {
        await TransactionWithRetriesPolicy.Execute<Task>(async (ct) =>
        {
            int deletedFileTransfers = await fileTransferRepository.HardDeleteFileTransfersByIds(fileTransferIds, cancellationToken);
            logger.LogInformation("Deleted {deletedFileTransfers} file transfers for resourceId {resourceId} with testTag {testTag}", deletedFileTransfers, resourceId, testTag);
			
            return Task.CompletedTask;
        }, logger, cancellationToken);
    }
}

