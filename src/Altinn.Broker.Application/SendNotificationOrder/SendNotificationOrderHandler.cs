using System.Text.Json;
using System.Transactions;

using Altinn.Broker.Application.CheckNotificationDelivery;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Models.Notifications;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;

using Hangfire;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Application.SendNotificationOrder;

public class SendNotificationOrderHandler(
    IFileTransferNotificationRepository fileTransferNotificationRepository,
    IAltinnNotificationService altinnNotificationService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<SendNotificationOrderHandler> logger)
{
    public async Task Process(Guid fileTransferId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting notification sending process for file transfer {FileTransferId}", fileTransferId);

        var notificationOrders = await fileTransferNotificationRepository.GetNotificationsForFileTransfer(fileTransferId, cancellationToken);
        // Rejected ones are left out: Altinn Notifications would refuse them again.
        var pendingOrders = notificationOrders
            .Where(order => order.Status == NotificationDeliveryStatus.Pending && order.NotificationOrderId is null && order.ShipmentId is null)
            .ToList();
        if (pendingOrders.Count == 0)
        {
            logger.LogInformation("No pending notification orders found for file transfer {FileTransferId}", fileTransferId);
            return;
        }
        logger.LogInformation("Sending {Count} notification order(s) for file transfer {FileTransferId}", pendingOrders.Count, fileTransferId);

        // One recipient failing must not keep the others from being ordered, so each is attempted before the job fails.
        // The job then throws, so Hangfire retries it - and a retry only picks up those still pending.
        var failures = new List<Exception>();
        foreach (var notificationOrder in pendingOrders)
        {
            try
            {
                await SendNotificationOrderForRecipient(notificationOrder, fileTransferId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "Failed to order notification {NotificationOrderId} for file transfer {FileTransferId}; the others are still attempted",
                    notificationOrder.Id,
                    fileTransferId);
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"Failed to order {failures.Count} of {pendingOrders.Count} notification(s) for file transfer {fileTransferId}",
                failures);
        }
    }

    private async Task SendNotificationOrderForRecipient(BrokerNotificationEntity notificationOrder, Guid fileTransferId, CancellationToken cancellationToken)
    {
        if (notificationOrder.OrderRequest is null)
        {
            logger.LogError(
                "No order request found for notification order {NotificationOrderId} on file transfer {FileTransferId}",
                notificationOrder.Id,
                fileTransferId);
            return;
        }

        NotificationOrderRequestV2? orderRequest;
        try
        {
            orderRequest = JsonSerializer.Deserialize<NotificationOrderRequestV2>(notificationOrder.OrderRequest);
        }
        catch (JsonException)
        {
            orderRequest = null;
        }
        if (orderRequest is null)
        {
            logger.LogError(
                "Failed to deserialize order request for notification order {NotificationOrderId} on file transfer {FileTransferId}",
                notificationOrder.Id,
                fileTransferId);
            return;
        }

        if (orderRequest.RequestedSendTime <= DateTime.UtcNow.AddSeconds(20))
        {
            orderRequest.RequestedSendTime = DateTime.UtcNow.AddSeconds(20);
        }

        logger.LogInformation("Sending notification order {IdempotencyId} for file transfer {FileTransferId}", orderRequest.IdempotencyId, fileTransferId);
        NotificationOrderResponseV2 notificationResponse;
        try
        {
            notificationResponse = await altinnNotificationService.CreateNotificationV2(orderRequest, cancellationToken);
        }
        catch (NotificationOrderRejectedException rejection)
        {
            // Not retried: the same order would be refused again. Recorded so the sender can see it was never sent - and
            // since the reminder is part of the same order, no reminder exists either.
            logger.LogWarning(
                "Altinn Notifications rejected notification order {NotificationOrderId} for file transfer {FileTransferId}: {Reason}",
                notificationOrder.Id,
                fileTransferId,
                rejection.Reason);
            await fileTransferNotificationRepository.UpdateStatus(notificationOrder.Id, NotificationDeliveryStatus.Rejected, rejection.Reason, cancellationToken);
            return;
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        await fileTransferNotificationRepository.UpdateOrderResponseData(
            notificationOrder.Id,
            notificationResponse.NotificationOrderId,
            notificationResponse.Notification.ShipmentId,
            cancellationToken);
        backgroundJobClient.Schedule<CheckNotificationDeliveryHandler>(
            handler => handler.Process(notificationOrder.Id, CancellationToken.None, 1),
            TimeSpan.FromSeconds(60));

        if (orderRequest.Reminders is { Count: > 0 })
        {
            foreach (var reminderResponse in notificationResponse.Notification.Reminders)
            {
                await PersistReminderNotification(notificationOrder, orderRequest, notificationResponse.NotificationOrderId, reminderResponse, cancellationToken);
            }
        }

        transaction.Complete();
    }

    private async Task PersistReminderNotification(
        BrokerNotificationEntity mainNotificationOrder,
        NotificationOrderRequestV2 orderRequest,
        Guid notificationOrderId,
        ReminderResponse reminderResponse,
        CancellationToken cancellationToken)
    {
        var reminderNotification = new BrokerNotificationEntity
        {
            Id = Guid.NewGuid(),
            FileTransferId = mainNotificationOrder.FileTransferId,
            ActorId = mainNotificationOrder.ActorId,
            CustomRecipientType = mainNotificationOrder.CustomRecipientType,
            CustomRecipientIdentifier = mainNotificationOrder.CustomRecipientIdentifier,
            CustomRecipientRelatedOrganization = mainNotificationOrder.CustomRecipientRelatedOrganization,
            NotificationTemplate = mainNotificationOrder.NotificationTemplate,
            NotificationChannel = NotificationChannel.EmailAndSms,
            RequestedSendTime = mainNotificationOrder.RequestedSendTime.AddDays(orderRequest.Reminders?.FirstOrDefault()?.DelayDays ?? 0),
            Created = DateTimeOffset.UtcNow,
            IsReminder = true,
            NotificationOrderId = notificationOrderId,
            ShipmentId = reminderResponse.ShipmentId,
            Status = NotificationDeliveryStatus.Ordered,
            OrderRequest = JsonSerializer.Serialize(orderRequest)
        };
        await fileTransferNotificationRepository.AddNotification(reminderNotification, cancellationToken);
        backgroundJobClient.Schedule<CheckNotificationDeliveryHandler>(
            handler => handler.Process(reminderNotification.Id, CancellationToken.None, 1),
            reminderNotification.RequestedSendTime.AddSeconds(60));
    }
}
