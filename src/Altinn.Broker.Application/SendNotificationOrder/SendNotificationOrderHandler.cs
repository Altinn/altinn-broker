using System.Text.Json;
using System.Transactions;

using Altinn.Broker.Application.CheckNotificationDelivery;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models.Notifications;
using Altinn.Broker.Core.Repositories;

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
        var pendingOrders = notificationOrders.Where(order => order.NotificationOrderId is null && order.ShipmentId is null).ToList();
        if (pendingOrders.Count == 0)
        {
            logger.LogInformation("No pending notification orders found for file transfer {FileTransferId}", fileTransferId);
            return;
        }
        logger.LogInformation("Sending {Count} notification order(s) for file transfer {FileTransferId}", pendingOrders.Count, fileTransferId);

        foreach (var notificationOrder in pendingOrders)
        {
            await SendNotificationOrderForRecipient(notificationOrder, fileTransferId, cancellationToken);
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
        var notificationResponse = await altinnNotificationService.CreateNotificationV2(orderRequest, cancellationToken);
        if (notificationResponse is null)
        {
            logger.LogError("Failed to create notification for file transfer {FileTransferId}, notification order {NotificationOrderId}", fileTransferId, notificationOrder.Id);
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
            CustomRecipient = mainNotificationOrder.CustomRecipient,
            NotificationTemplate = mainNotificationOrder.NotificationTemplate,
            NotificationChannel = mainNotificationOrder.NotificationChannel,
            RequestedSendTime = mainNotificationOrder.RequestedSendTime.AddDays(orderRequest.Reminders?.FirstOrDefault()?.DelayDays ?? 0),
            Created = DateTimeOffset.UtcNow,
            IsReminder = true,
            NotificationOrderId = notificationOrderId,
            ShipmentId = reminderResponse.ShipmentId,
            OrderRequest = JsonSerializer.Serialize(orderRequest)
        };
        await fileTransferNotificationRepository.AddNotification(reminderNotification, cancellationToken);
        backgroundJobClient.Schedule<CheckNotificationDeliveryHandler>(
            handler => handler.Process(reminderNotification.Id, CancellationToken.None, 1),
            reminderNotification.RequestedSendTime.AddSeconds(60));
    }
}
