using Altinn.Broker.Application.CleanupUseCaseTests;
using Altinn.Broker.Core.Repositories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Altinn.Broker.Tests;

public class CleanupUseCaseTestsHandlerTests
{
	private const string ResourceId = "bruksmonster-broker";
	private const string TestTagPropertyKey = "testTag";
	private const string TestTagA3 = "unitTestsA3";
	private const string TestTagLegacy = "unitTestsLegacy";

	private static CleanupUseCaseTestsHandler CreateHandler(
		Mock<IBackgroundJobClient> bgClientMock,
		Mock<IFileTransferRepository> repoMock)
	{
		var loggerMock = new Mock<ILogger<CleanupUseCaseTestsHandler>>();
		return new CleanupUseCaseTestsHandler(bgClientMock.Object, loggerMock.Object, repoMock.Object);
	}

	[Fact]
	public async Task Process_EnqueuesDeleteJob_ReturnsResponseWithCounts()
	{
		// Arrange
		var existingIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

		var repoMock = new Mock<IFileTransferRepository>();
		repoMock.Setup(r => r.GetFileTransfersByPropertyTag(ResourceId, TestTagPropertyKey, TestTagA3, It.IsAny<CancellationToken>()))
			.ReturnsAsync(existingIds);

		Job? capturedJob = null;
		var bgClientMock = new Mock<IBackgroundJobClient>();
		bgClientMock
			.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
			.Callback<Job, IState>((job, state) => capturedJob = job)
			.Returns("job-123");

		var handler = CreateHandler(bgClientMock, repoMock);
		var request = new CleanupUseCaseTestsRequest { TestTag = TestTagA3 };

		// Act
		var result = await handler.Process(request, null, CancellationToken.None);

		// Assert
		var response = result.AsT0; 
		Assert.Equal(ResourceId, response.ResourceId);
		Assert.Equal(TestTagA3, response.TestTag);
		Assert.Equal(existingIds.Count, response.FileTransfersFound);
		Assert.Equal("job-123", response.DeleteFileTransfersJobId);

		bgClientMock.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
		repoMock.Verify(r => r.GetFileTransfersByPropertyTag(ResourceId, TestTagPropertyKey, TestTagA3, It.IsAny<CancellationToken>()), Times.Once);

		// Validate job target & args
		Assert.NotNull(capturedJob);
		Assert.Equal(typeof(CleanupUseCaseTestsHandler), capturedJob!.Type);
		Assert.Equal(nameof(CleanupUseCaseTestsHandler.DeleteFileTransfers), capturedJob.Method.Name);
		var argFileTransfersVal = capturedJob.Args[0] as List<Guid>;
		var argResourceVal = capturedJob.Args[1] as string;
		var argTestTagVal = capturedJob.Args[2] as string;
		Assert.NotNull(argFileTransfersVal);
		Assert.Equal(existingIds.OrderBy(x => x), argFileTransfersVal!.OrderBy(x => x));
		Assert.Equal(ResourceId, argResourceVal);
		Assert.Equal(TestTagA3, argTestTagVal);
	}

	[Fact]
	public async Task Process_EmptyList_StillEnqueuesJobWithEmptyIds()
	{
		// Arrange
		var repoMock = new Mock<IFileTransferRepository>();
		repoMock.Setup(r => r.GetFileTransfersByPropertyTag(ResourceId, TestTagPropertyKey, TestTagLegacy, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<Guid>());

		Job? capturedJob = null;
		var bgClientMock = new Mock<IBackgroundJobClient>();
		bgClientMock
			.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
			.Callback<Job, IState>((job, state) => capturedJob = job)
			.Returns("job-empty");

		var handler = CreateHandler(bgClientMock, repoMock);
		var request = new CleanupUseCaseTestsRequest { TestTag = TestTagLegacy };

		// Act
		var result = await handler.Process(request, null, CancellationToken.None);

		// Assert
		var response = result.AsT0;
		Assert.Equal(ResourceId, response.ResourceId);
		Assert.Equal(TestTagLegacy, response.TestTag);
		Assert.Equal(0, response.FileTransfersFound);
		Assert.Equal("job-empty", response.DeleteFileTransfersJobId);
		bgClientMock.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
        repoMock.Verify(
            r => r.GetFileTransfersByPropertyTag(ResourceId, TestTagPropertyKey, TestTagLegacy, It.IsAny<CancellationToken>()),
    Times.Once);
		Assert.NotNull(capturedJob);
		var argFileTransfersVal = capturedJob!.Args[0] as List<Guid>;
		Assert.NotNull(argFileTransfersVal);
		Assert.Empty(argFileTransfersVal!);
	}

	[Fact]
	public async Task DeleteFileTransfers_InvokesRepositoryHardDelete()
	{
		// Arrange
		var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
		var repoMock = new Mock<IFileTransferRepository>();
		repoMock.Setup(r => r.HardDeleteFileTransfersByIds(ids, It.IsAny<CancellationToken>()))
			.ReturnsAsync(ids.Count);

		var bgClientMock = new Mock<IBackgroundJobClient>();
		var handler = CreateHandler(bgClientMock, repoMock);

		// Act
		await handler.DeleteFileTransfers(ids, ResourceId, TestTagA3, CancellationToken.None);

		// Assert
		repoMock.Verify(r => r.HardDeleteFileTransfersByIds(ids, It.IsAny<CancellationToken>()), Times.Once);
	}
}
