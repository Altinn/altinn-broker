using Altinn.Broker.Application.CheckNotificationDelivery;
using Altinn.Broker.Application.SendSlackNotification;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Models.Notifications;
using Altinn.Broker.Core.Repositories;

using Hangfire;
using Hangfire.Common;
using Hangfire.States;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class CheckNotificationDeliveryHandlerTests
{
    private static BrokerNotificationEntity CreateNotification(
        Guid? id = null,
        Guid? shipmentId = null,
        DateTimeOffset? notificationSent = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        FileTransferId = Guid.NewGuid(),
        NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
        NotificationChannel = NotificationChannel.Email,
        RequestedSendTime = DateTimeOffset.UtcNow,
        Created = DateTimeOffset.UtcNow,
        ShipmentId = shipmentId ?? Guid.NewGuid(),
        NotificationSent = notificationSent
    };

    private static NotificationStatusResponseV2 CreateStatusResponse(string status, List<RecipientStatus> recipients) => new()
    {
        ShipmentId = Guid.NewGuid(),
        SendersReference = "ref",
        Type = "Notification",
        Status = status,
        LastUpdate = DateTimeOffset.UtcNow,
        Recipients = recipients
    };

    private static RecipientStatus CreateRecipient(NotificationType type, NotificationStatusV2 status, string destination, DateTimeOffset lastUpdate) => new()
    {
        Type = type,
        Status = status,
        Destination = destination,
        LastUpdate = lastUpdate
    };

    private static (Mock<IFileTransferNotificationRepository> Repository, Mock<IAltinnNotificationService> NotificationService, Mock<IBackgroundJobClient> BackgroundJobClient, List<(Job Job, IState State)> CapturedJobs, CheckNotificationDeliveryHandler Handler) CreateHandler()
    {
        var repository = new Mock<IFileTransferNotificationRepository>();
        var notificationService = new Mock<IAltinnNotificationService>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();
        var capturedJobs = new List<(Job, IState)>();
        backgroundJobClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((job, state) => capturedJobs.Add((job, state)))
            .Returns("job-id");
        var logger = new Mock<ILogger<CheckNotificationDeliveryHandler>>();

        var handler = new CheckNotificationDeliveryHandler(
            repository.Object,
            notificationService.Object,
            backgroundJobClient.Object,
            logger.Object);

        return (repository, notificationService, backgroundJobClient, capturedJobs, handler);
    }

    [Fact]
    public async Task Process_WhenNotificationNotFound_DoesNotCallNotificationService()
    {
        var (repository, notificationService, _, capturedJobs, handler) = CreateHandler();
        var notificationId = Guid.NewGuid();
        repository.Setup(r => r.GetNotificationById(notificationId, It.IsAny<CancellationToken>())).ReturnsAsync((BrokerNotificationEntity?)null);

        await handler.Process(notificationId, CancellationToken.None);

        notificationService.Verify(s => s.GetNotificationDetailsV2(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(capturedJobs);
    }

    [Fact]
    public async Task Process_WhenAlreadySent_IsIdempotentAndDoesNotCallNotificationService()
    {
        var (repository, notificationService, _, capturedJobs, handler) = CreateHandler();
        var notification = CreateNotification(notificationSent: DateTimeOffset.UtcNow);
        repository.Setup(r => r.GetNotificationById(notification.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notification);

        await handler.Process(notification.Id, CancellationToken.None);

        notificationService.Verify(s => s.GetNotificationDetailsV2(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(capturedJobs);
    }

    [Fact]
    public async Task Process_WhenShipmentIdMissing_DoesNotCallNotificationService()
    {
        var (repository, notificationService, _, capturedJobs, handler) = CreateHandler();
        var notification = CreateNotification();
        notification.ShipmentId = null;
        repository.Setup(r => r.GetNotificationById(notification.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notification);

        await handler.Process(notification.Id, CancellationToken.None);

        notificationService.Verify(s => s.GetNotificationDetailsV2(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(capturedJobs);
    }

    [Fact]
    public async Task Process_WhenStatusIsNotFinal_SchedulesRetryWithFirstBackoffDelay()
    {
        var (repository, notificationService, _, capturedJobs, handler) = CreateHandler();
        var notification = CreateNotification();
        repository.Setup(r => r.GetNotificationById(notification.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notification);
        notificationService
            .Setup(s => s.GetNotificationDetailsV2(notification.ShipmentId!.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStatusResponse("Order_Processing", []));

        await handler.Process(notification.Id, CancellationToken.None, attempt: 1);

        var scheduled = Assert.Single(capturedJobs);
        Assert.Equal(typeof(CheckNotificationDeliveryHandler), scheduled.Job.Type);
        Assert.Equal(nameof(CheckNotificationDeliveryHandler.Process), scheduled.Job.Method.Name);
        Assert.Equal(notification.Id, scheduled.Job.Args[0]);
        Assert.Equal(2, scheduled.Job.Args[2]);
        var scheduledState = Assert.IsType<ScheduledState>(scheduled.State);
        Assert.True(scheduledState.EnqueueAt > DateTime.UtcNow.AddSeconds(55) && scheduledState.EnqueueAt <= DateTime.UtcNow.AddSeconds(61));
        repository.Verify(r => r.UpdateNotificationSent(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenStatusIsNotFinalAtMaxAttempts_GivesUpAndAlertsSlack()
    {
        var (repository, notificationService, _, capturedJobs, handler) = CreateHandler();
        var notification = CreateNotification();
        repository.Setup(r => r.GetNotificationById(notification.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notification);
        notificationService
            .Setup(s => s.GetNotificationDetailsV2(notification.ShipmentId!.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStatusResponse("Order_Processing", []));

        await handler.Process(notification.Id, CancellationToken.None, attempt: 10);

        var slackJob = Assert.Single(capturedJobs);
        Assert.Equal(typeof(SendSlackNotificationHandler), slackJob.Job.Type);
        Assert.Equal(nameof(SendSlackNotificationHandler.Process), slackJob.Job.Method.Name);
        Assert.IsType<EnqueuedState>(slackJob.State);
        repository.Verify(r => r.UpdateNotificationSent(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenFinalStatusWithSentAndFailedRecipients_UpdatesSentUsingOnlySentRecipient()
    {
        var (repository, notificationService, _, capturedJobs, handler) = CreateHandler();
        var notification = CreateNotification();
        repository.Setup(r => r.GetNotificationById(notification.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notification);
        var sentTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var statusResponse = CreateStatusResponse("Order_Completed",
        [
            CreateRecipient(NotificationType.Email, NotificationStatusV2.Email_Delivered, "sent@example.com", sentTime),
            CreateRecipient(NotificationType.Email, NotificationStatusV2.Email_Failed, "failed@example.com", DateTimeOffset.UtcNow)
        ]);
        notificationService
            .Setup(s => s.GetNotificationDetailsV2(notification.ShipmentId!.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusResponse);

        await handler.Process(notification.Id, CancellationToken.None);

        repository.Verify(r => r.UpdateNotificationSent(notification.Id, sentTime, "sent@example.com", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(capturedJobs);
    }

    [Fact]
    public async Task Process_WhenFinalStatusWithMultipleSentRecipients_UsesEarliestLastUpdateAndJoinsDestinations()
    {
        var (repository, notificationService, _, _, handler) = CreateHandler();
        var notification = CreateNotification();
        repository.Setup(r => r.GetNotificationById(notification.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notification);
        var earlier = DateTimeOffset.UtcNow.AddMinutes(-10);
        var later = DateTimeOffset.UtcNow.AddMinutes(-2);
        var statusResponse = CreateStatusResponse("Order_Completed",
        [
            CreateRecipient(NotificationType.Email, NotificationStatusV2.Email_Delivered, "first@example.com", later),
            CreateRecipient(NotificationType.SMS, NotificationStatusV2.SMS_Delivered, "+4791234567", earlier)
        ]);
        notificationService
            .Setup(s => s.GetNotificationDetailsV2(notification.ShipmentId!.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusResponse);

        await handler.Process(notification.Id, CancellationToken.None);

        repository.Verify(r => r.UpdateNotificationSent(notification.Id, earlier, "first@example.com, +4791234567", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_WhenFinalStatusWithNoSentRecipients_DoesNotUpdateNotificationSent()
    {
        var (repository, notificationService, _, capturedJobs, handler) = CreateHandler();
        var notification = CreateNotification();
        repository.Setup(r => r.GetNotificationById(notification.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notification);
        var statusResponse = CreateStatusResponse("Order_Completed",
        [
            CreateRecipient(NotificationType.Email, NotificationStatusV2.Email_Failed, "failed@example.com", DateTimeOffset.UtcNow)
        ]);
        notificationService
            .Setup(s => s.GetNotificationDetailsV2(notification.ShipmentId!.Value.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusResponse);

        await handler.Process(notification.Id, CancellationToken.None);

        repository.Verify(r => r.UpdateNotificationSent(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(capturedJobs);
    }
}
