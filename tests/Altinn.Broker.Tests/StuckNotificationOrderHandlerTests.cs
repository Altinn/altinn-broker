using Altinn.Broker.Application.SendNotificationOrder;
using Altinn.Broker.Application.SendSlackNotification;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Repositories;

using Hangfire;
using Hangfire.Common;
using Hangfire.States;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class StuckNotificationOrderHandlerTests
{
    private static (Mock<IFileTransferNotificationRepository> Repository, List<Job> EnqueuedJobs, StuckNotificationOrderHandler Handler) CreateHandler(
        params FileTransferWithPendingNotifications[] fileTransfers)
    {
        var repository = new Mock<IFileTransferNotificationRepository>();
        repository
            .Setup(r => r.GetFileTransfersWithPendingNotifications(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileTransfers.ToList());
        var backgroundJobClient = new Mock<IBackgroundJobClient>();
        var enqueuedJobs = new List<Job>();
        backgroundJobClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((job, _) => enqueuedJobs.Add(job))
            .Returns("job-id");

        var handler = new StuckNotificationOrderHandler(repository.Object, backgroundJobClient.Object, new Mock<ILogger<StuckNotificationOrderHandler>>().Object);
        return (repository, enqueuedJobs, handler);
    }

    private static FileTransferWithPendingNotifications FileTransfer(TimeSpan publishedAgo, FileTransferStatus? status = FileTransferStatus.Published) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow - publishedAgo, status);

    [Fact]
    public async Task Process_OnlyLooksAtFileTransfersPublishedBeforeTheRetryThreshold()
    {
        var (repository, _, handler) = CreateHandler();

        await handler.Process(CancellationToken.None);

        repository.Verify(r => r.GetFileTransfersWithPendingNotifications(
            It.Is<DateTimeOffset>(publishedBefore => publishedBefore <= DateTimeOffset.UtcNow - StuckNotificationOrderHandler.RetryAfter
                && publishedBefore > DateTimeOffset.UtcNow - StuckNotificationOrderHandler.RetryAfter - TimeSpan.FromMinutes(1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(FileTransferStatus.Published)]
    [InlineData(null)]
    public async Task Process_WhenStillPublishedWithinGiveUpWindow_OrdersAgain(FileTransferStatus? status)
    {
        var fileTransfer = FileTransfer(TimeSpan.FromDays(1), status);
        var (repository, enqueuedJobs, handler) = CreateHandler(fileTransfer);

        await handler.Process(CancellationToken.None);

        var job = Assert.Single(enqueuedJobs);
        Assert.Equal(typeof(SendNotificationOrderHandler), job.Type);
        Assert.Equal(fileTransfer.FileTransferId, job.Args[0]);
        repository.Verify(r => r.UpdatePendingStatusesForFileTransfer(It.IsAny<Guid>(), It.IsAny<NotificationDeliveryStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenPublishedLongerAgoThanGiveUpWindow_MarksOrderUnconfirmedAndAlertsSlack()
    {
        var fileTransfer = FileTransfer(StuckNotificationOrderHandler.GiveUpAfter + TimeSpan.FromHours(1));
        var (repository, enqueuedJobs, handler) = CreateHandler(fileTransfer);

        await handler.Process(CancellationToken.None);

        repository.Verify(r => r.UpdatePendingStatusesForFileTransfer(fileTransfer.FileTransferId, NotificationDeliveryStatus.OrderUnconfirmed, It.IsNotNull<string>(), It.IsAny<CancellationToken>()), Times.Once);
        var job = Assert.Single(enqueuedJobs);
        Assert.Equal(typeof(SendSlackNotificationHandler), job.Type);
    }

    [Theory]
    [InlineData(FileTransferStatus.AllConfirmedDownloaded)]
    [InlineData(FileTransferStatus.Cancelled)]
    [InlineData(FileTransferStatus.Failed)]
    public async Task Process_WhenFileTransferNoLongerPublished_MarksOrderUnconfirmedWithoutOrdering(FileTransferStatus status)
    {
        var fileTransfer = FileTransfer(TimeSpan.FromDays(1), status);
        var (repository, enqueuedJobs, handler) = CreateHandler(fileTransfer);

        await handler.Process(CancellationToken.None);

        repository.Verify(r => r.UpdatePendingStatusesForFileTransfer(fileTransfer.FileTransferId, NotificationDeliveryStatus.OrderUnconfirmed, It.IsNotNull<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(enqueuedJobs);
    }

    [Fact]
    public async Task Process_HandlesEachFileTransferOnItsOwn()
    {
        var toRetry = FileTransfer(TimeSpan.FromDays(1));
        var toGiveUpOn = FileTransfer(StuckNotificationOrderHandler.GiveUpAfter + TimeSpan.FromHours(1));
        var (repository, enqueuedJobs, handler) = CreateHandler(toRetry, toGiveUpOn);

        await handler.Process(CancellationToken.None);

        Assert.Contains(enqueuedJobs, job => job.Type == typeof(SendNotificationOrderHandler) && Equals(job.Args[0], toRetry.FileTransferId));
        repository.Verify(r => r.UpdatePendingStatusesForFileTransfer(toGiveUpOn.FileTransferId, NotificationDeliveryStatus.OrderUnconfirmed, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.UpdatePendingStatusesForFileTransfer(toRetry.FileTransferId, It.IsAny<NotificationDeliveryStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
