using System.Text.Json;

using Altinn.Broker.Application.CheckNotificationDelivery;
using Altinn.Broker.Application.SendNotificationOrder;
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

public class SendNotificationOrderHandlerTests
{
    private static BrokerNotificationEntity CreateOrder(
        Guid? id = null,
        Guid? fileTransferId = null,
        string? orderRequest = null,
        Guid? notificationOrderId = null,
        Guid? shipmentId = null,
        long? actorId = 42) => new()
    {
        Id = id ?? Guid.NewGuid(),
        FileTransferId = fileTransferId ?? Guid.NewGuid(),
        ActorId = actorId,
        NotificationTemplate = NotificationTemplate.CustomMessage,
        NotificationChannel = NotificationChannel.Email,
        RequestedSendTime = DateTimeOffset.UtcNow,
        Created = DateTimeOffset.UtcNow,
        OrderRequest = orderRequest,
        NotificationOrderId = notificationOrderId,
        ShipmentId = shipmentId
    };

    private static NotificationOrderRequestV2 CreateOrderRequest(DateTime? requestedSendTime = null, List<ReminderV2>? reminders = null) => new()
    {
        IdempotencyId = Guid.NewGuid(),
        RequestedSendTime = requestedSendTime ?? DateTime.UtcNow,
        Recipient = new RecipientV2 { RecipientEmail = new RecipientEmail { EmailAddress = "test@example.com" } },
        Reminders = reminders
    };

    private static (Mock<IFileTransferNotificationRepository> Repository, Mock<IAltinnNotificationService> NotificationService, Mock<IBackgroundJobClient> BackgroundJobClient, List<(Job Job, IState State)> CapturedJobs, SendNotificationOrderHandler Handler) CreateHandler()
    {
        var repository = new Mock<IFileTransferNotificationRepository>();
        var notificationService = new Mock<IAltinnNotificationService>();
        var backgroundJobClient = new Mock<IBackgroundJobClient>();
        var capturedJobs = new List<(Job, IState)>();
        backgroundJobClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Callback<Job, IState>((job, state) => capturedJobs.Add((job, state)))
            .Returns("job-id");
        var logger = new Mock<ILogger<SendNotificationOrderHandler>>();

        var handler = new SendNotificationOrderHandler(repository.Object, notificationService.Object, backgroundJobClient.Object, logger.Object);

        return (repository, notificationService, backgroundJobClient, capturedJobs, handler);
    }

    [Fact]
    public async Task Process_WhenNoOrdersExist_DoesNotCallNotificationService()
    {
        var (repository, notificationService, _, capturedJobs, handler) = CreateHandler();
        var fileTransferId = Guid.NewGuid();
        repository.Setup(r => r.GetNotificationsForFileTransfer(fileTransferId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await handler.Process(fileTransferId, CancellationToken.None);

        notificationService.Verify(s => s.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(capturedJobs);
    }

    [Fact]
    public async Task Process_WhenOrdersExistButNoneArePending_DoesNotCallNotificationService()
    {
        var (repository, notificationService, _, capturedJobs, handler) = CreateHandler();
        var fileTransferId = Guid.NewGuid();
        var alreadySent = CreateOrder(fileTransferId: fileTransferId, notificationOrderId: Guid.NewGuid(), shipmentId: Guid.NewGuid());
        repository.Setup(r => r.GetNotificationsForFileTransfer(fileTransferId, It.IsAny<CancellationToken>())).ReturnsAsync([alreadySent]);

        await handler.Process(fileTransferId, CancellationToken.None);

        notificationService.Verify(s => s.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(capturedJobs);
    }

    [Fact]
    public async Task Process_WhenOrderAlreadySent_SkipsIt()
    {
        var (repository, notificationService, _, _, handler) = CreateHandler();
        var fileTransferId = Guid.NewGuid();
        var alreadySent = CreateOrder(fileTransferId: fileTransferId, notificationOrderId: Guid.NewGuid(), shipmentId: Guid.NewGuid());
        repository.Setup(r => r.GetNotificationsForFileTransfer(fileTransferId, It.IsAny<CancellationToken>())).ReturnsAsync([alreadySent]);

        await handler.Process(fileTransferId, CancellationToken.None);

        notificationService.Verify(s => s.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenOrderRequestIsNull_LogsAndSkipsWithoutThrowing()
    {
        var (repository, notificationService, _, _, handler) = CreateHandler();
        var fileTransferId = Guid.NewGuid();
        var order = CreateOrder(fileTransferId: fileTransferId, orderRequest: null);
        repository.Setup(r => r.GetNotificationsForFileTransfer(fileTransferId, It.IsAny<CancellationToken>())).ReturnsAsync([order]);

        await handler.Process(fileTransferId, CancellationToken.None);

        notificationService.Verify(s => s.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenOrderRequestFailsToDeserialize_LogsAndSkipsWithoutThrowing()
    {
        var (repository, notificationService, _, _, handler) = CreateHandler();
        var fileTransferId = Guid.NewGuid();
        var order = CreateOrder(fileTransferId: fileTransferId, orderRequest: "not valid json");
        repository.Setup(r => r.GetNotificationsForFileTransfer(fileTransferId, It.IsAny<CancellationToken>())).ReturnsAsync([order]);

        await handler.Process(fileTransferId, CancellationToken.None);

        notificationService.Verify(s => s.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenRequestedSendTimeIsInThePast_BumpsItToAtLeastTwentySecondsFromNow()
    {
        var (repository, notificationService, _, _, handler) = CreateHandler();
        var fileTransferId = Guid.NewGuid();
        var pastSendTime = DateTime.UtcNow.AddHours(-1);
        var orderRequest = CreateOrderRequest(requestedSendTime: pastSendTime);
        var order = CreateOrder(fileTransferId: fileTransferId, orderRequest: JsonSerializer.Serialize(orderRequest));
        repository.Setup(r => r.GetNotificationsForFileTransfer(fileTransferId, It.IsAny<CancellationToken>())).ReturnsAsync([order]);
        NotificationOrderRequestV2? capturedRequest = null;
        notificationService
            .Setup(s => s.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationOrderRequestV2, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync((NotificationOrderResponseV2?)null);

        await handler.Process(fileTransferId, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest!.RequestedSendTime >= DateTime.UtcNow.AddSeconds(19));
    }

    [Fact]
    public async Task Process_WhenNotificationServiceReturnsNull_DoesNotUpdateOrScheduleDeliveryCheck()
    {
        var (repository, notificationService, _, capturedJobs, handler) = CreateHandler();
        var fileTransferId = Guid.NewGuid();
        var order = CreateOrder(fileTransferId: fileTransferId, orderRequest: JsonSerializer.Serialize(CreateOrderRequest()));
        repository.Setup(r => r.GetNotificationsForFileTransfer(fileTransferId, It.IsAny<CancellationToken>())).ReturnsAsync([order]);
        notificationService
            .Setup(s => s.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOrderResponseV2?)null);

        await handler.Process(fileTransferId, CancellationToken.None);

        repository.Verify(r => r.UpdateOrderResponseData(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(capturedJobs);
    }

    [Fact]
    public async Task Process_WhenSuccessful_UpdatesOrderResponseDataAndSchedulesDeliveryCheck()
    {
        var (repository, notificationService, _, capturedJobs, handler) = CreateHandler();
        var fileTransferId = Guid.NewGuid();
        var order = CreateOrder(fileTransferId: fileTransferId, orderRequest: JsonSerializer.Serialize(CreateOrderRequest()));
        repository.Setup(r => r.GetNotificationsForFileTransfer(fileTransferId, It.IsAny<CancellationToken>())).ReturnsAsync([order]);
        var notificationOrderId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        var response = new NotificationOrderResponseV2
        {
            NotificationOrderId = notificationOrderId,
            Notification = new NotificationResponseV2 { ShipmentId = shipmentId, SendersReference = "ref" }
        };
        notificationService
            .Setup(s => s.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        await handler.Process(fileTransferId, CancellationToken.None);

        repository.Verify(r => r.UpdateOrderResponseData(order.Id, notificationOrderId, shipmentId, It.IsAny<CancellationToken>()), Times.Once);
        var scheduled = Assert.Single(capturedJobs);
        Assert.Equal(typeof(CheckNotificationDeliveryHandler), scheduled.Job.Type);
        Assert.Equal(order.Id, scheduled.Job.Args[0]);
        Assert.Equal(1, scheduled.Job.Args[2]);
        var scheduledState = Assert.IsType<ScheduledState>(scheduled.State);
        Assert.True(scheduledState.EnqueueAt > DateTime.UtcNow.AddSeconds(55) && scheduledState.EnqueueAt <= DateTime.UtcNow.AddSeconds(61));
    }

    [Fact]
    public async Task Process_WhenOrderHasReminders_PersistsReminderAndSchedulesItsOwnDeliveryCheck()
    {
        var (repository, notificationService, _, capturedJobs, handler) = CreateHandler();
        var fileTransferId = Guid.NewGuid();
        var mainRequestedSendTime = DateTimeOffset.UtcNow;
        var orderRequest = CreateOrderRequest(reminders: [new ReminderV2 { DelayDays = 7 }]);
        var order = CreateOrder(fileTransferId: fileTransferId, orderRequest: JsonSerializer.Serialize(orderRequest));
        order.RequestedSendTime = mainRequestedSendTime;
        order.ActorId = 99;
        repository.Setup(r => r.GetNotificationsForFileTransfer(fileTransferId, It.IsAny<CancellationToken>())).ReturnsAsync([order]);
        var notificationOrderId = Guid.NewGuid();
        var reminderShipmentId = Guid.NewGuid();
        var response = new NotificationOrderResponseV2
        {
            NotificationOrderId = notificationOrderId,
            Notification = new NotificationResponseV2
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = "ref",
                Reminders = [new ReminderResponse { ShipmentId = reminderShipmentId, SendersReference = "reminder-ref" }]
            }
        };
        notificationService
            .Setup(s => s.CreateNotificationV2(It.IsAny<NotificationOrderRequestV2>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        BrokerNotificationEntity? persistedReminder = null;
        repository
            .Setup(r => r.AddNotification(It.Is<BrokerNotificationEntity>(n => n.IsReminder), It.IsAny<CancellationToken>()))
            .Callback<BrokerNotificationEntity, CancellationToken>((n, _) => persistedReminder = n)
            .Returns(Task.CompletedTask);

        await handler.Process(fileTransferId, CancellationToken.None);

        Assert.NotNull(persistedReminder);
        Assert.Equal(fileTransferId, persistedReminder!.FileTransferId);
        Assert.Equal(99, persistedReminder.ActorId);
        Assert.Equal(notificationOrderId, persistedReminder.NotificationOrderId);
        Assert.Equal(reminderShipmentId, persistedReminder.ShipmentId);
        Assert.Equal(mainRequestedSendTime.AddDays(7), persistedReminder.RequestedSendTime);

        // One scheduled check for the main order, one for the reminder.
        Assert.Equal(2, capturedJobs.Count);
        var reminderCheck = capturedJobs.Single(j => (Guid)j.Job.Args[0] == persistedReminder.Id);
        var reminderScheduledState = Assert.IsType<ScheduledState>(reminderCheck.State);
        var expectedEnqueueAt = mainRequestedSendTime.AddDays(7).AddSeconds(60).UtcDateTime;
        Assert.True(Math.Abs((reminderScheduledState.EnqueueAt - expectedEnqueueAt).TotalSeconds) < 5);
    }
}
