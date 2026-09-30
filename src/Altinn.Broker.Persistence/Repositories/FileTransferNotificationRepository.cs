using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Models.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence.Helpers;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;

public class FileTransferNotificationRepository(NpgsqlDataSource dataSource, ExecuteDBCommandWithRetries commandExecutor) : IFileTransferNotificationRepository
{
    public async Task AddNotification(BrokerNotificationEntity notification, CancellationToken cancellationToken)
    {
        var query = @"
            INSERT INTO broker.file_transfer_notification (
                file_transfer_notification_id_pk,
                file_transfer_id_fk,
                actor_id_fk,
                custom_recipient,
                notification_template,
                notification_channel,
                requested_send_time,
                created,
                is_reminder,
                notification_order_id,
                shipment_id,
                order_request
            )
            VALUES (
                @id,
                @fileTransferId,
                @actorId,
                @customRecipient,
                @notificationTemplate,
                @notificationChannel,
                @requestedSendTime,
                @created,
                @isReminder,
                @notificationOrderId,
                @shipmentId,
                @orderRequest
            );";

        await using var command = dataSource.CreateCommand(query);
        command.Parameters.AddWithValue("@id", notification.Id);
        command.Parameters.AddWithValue("@fileTransferId", notification.FileTransferId);
        command.Parameters.AddWithValue("@actorId", (object?)notification.ActorId ?? DBNull.Value);
        command.Parameters.AddWithValue("@customRecipient", (object?)notification.CustomRecipient ?? DBNull.Value);
        command.Parameters.AddWithValue("@notificationTemplate", (int)notification.NotificationTemplate);
        command.Parameters.AddWithValue("@notificationChannel", (int)notification.NotificationChannel);
        command.Parameters.AddWithValue("@requestedSendTime", notification.RequestedSendTime);
        command.Parameters.AddWithValue("@created", notification.Created);
        command.Parameters.AddWithValue("@isReminder", notification.IsReminder);
        command.Parameters.AddWithValue("@notificationOrderId", (object?)notification.NotificationOrderId ?? DBNull.Value);
        command.Parameters.AddWithValue("@shipmentId", (object?)notification.ShipmentId ?? DBNull.Value);
        command.Parameters.AddWithValue("@orderRequest", (object?)notification.OrderRequest ?? DBNull.Value);

        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }

    public async Task<List<BrokerNotificationEntity>> GetNotificationsForFileTransfer(Guid fileTransferId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT * FROM broker.file_transfer_notification WHERE file_transfer_id_fk = @fileTransferId");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var notifications = new List<BrokerNotificationEntity>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                notifications.Add(new BrokerNotificationEntity()
                {
                    Id = reader.GetGuid(reader.GetOrdinal("file_transfer_notification_id_pk")),
                    FileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_fk")),
                    ActorId = reader.IsDBNull(reader.GetOrdinal("actor_id_fk")) ? null : reader.GetInt64(reader.GetOrdinal("actor_id_fk")),
                    CustomRecipient = reader.IsDBNull(reader.GetOrdinal("custom_recipient")) ? null : reader.GetString(reader.GetOrdinal("custom_recipient")),
                    NotificationTemplate = (NotificationTemplate)reader.GetInt32(reader.GetOrdinal("notification_template")),
                    NotificationChannel = (NotificationChannel)reader.GetInt32(reader.GetOrdinal("notification_channel")),
                    RequestedSendTime = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("requested_send_time")), DateTimeKind.Utc),
                    Created = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("created")), DateTimeKind.Utc),
                    IsReminder = reader.GetBoolean(reader.GetOrdinal("is_reminder")),
                    NotificationSent = reader.IsDBNull(reader.GetOrdinal("notification_sent")) ? null : DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("notification_sent")), DateTimeKind.Utc),
                    NotificationAddress = reader.IsDBNull(reader.GetOrdinal("notification_address")) ? null : reader.GetString(reader.GetOrdinal("notification_address")),
                    NotificationOrderId = reader.IsDBNull(reader.GetOrdinal("notification_order_id")) ? null : reader.GetGuid(reader.GetOrdinal("notification_order_id")),
                    ShipmentId = reader.IsDBNull(reader.GetOrdinal("shipment_id")) ? null : reader.GetGuid(reader.GetOrdinal("shipment_id")),
                    OrderRequest = reader.IsDBNull(reader.GetOrdinal("order_request")) ? null : reader.GetString(reader.GetOrdinal("order_request"))
                });
            }
            return notifications;
        }, cancellationToken);
    }

    public async Task<bool> HasNotificationsForFileTransfer(Guid fileTransferId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT EXISTS(SELECT 1 FROM broker.file_transfer_notification WHERE file_transfer_id_fk = @fileTransferId)");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var result = await command.ExecuteScalarAsync(ct);
            return result is bool exists && exists;
        }, cancellationToken);
    }

    public async Task UpdateOrderResponseData(Guid notificationId, Guid notificationOrderId, Guid shipmentId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.file_transfer_notification " +
            "SET notification_order_id = @notificationOrderId, shipment_id = @shipmentId " +
            "WHERE file_transfer_notification_id_pk = @id");
        command.Parameters.AddWithValue("@id", notificationId);
        command.Parameters.AddWithValue("@notificationOrderId", notificationOrderId);
        command.Parameters.AddWithValue("@shipmentId", shipmentId);

        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }

    public async Task<BrokerNotificationEntity?> GetNotificationById(Guid notificationId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT * FROM broker.file_transfer_notification WHERE file_transfer_notification_id_pk = @id");
        command.Parameters.AddWithValue("@id", notificationId);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }
            return new BrokerNotificationEntity()
            {
                Id = reader.GetGuid(reader.GetOrdinal("file_transfer_notification_id_pk")),
                FileTransferId = reader.GetGuid(reader.GetOrdinal("file_transfer_id_fk")),
                ActorId = reader.IsDBNull(reader.GetOrdinal("actor_id_fk")) ? null : reader.GetInt64(reader.GetOrdinal("actor_id_fk")),
                CustomRecipient = reader.IsDBNull(reader.GetOrdinal("custom_recipient")) ? null : reader.GetString(reader.GetOrdinal("custom_recipient")),
                NotificationTemplate = (NotificationTemplate)reader.GetInt32(reader.GetOrdinal("notification_template")),
                NotificationChannel = (NotificationChannel)reader.GetInt32(reader.GetOrdinal("notification_channel")),
                RequestedSendTime = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("requested_send_time")), DateTimeKind.Utc),
                Created = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("created")), DateTimeKind.Utc),
                IsReminder = reader.GetBoolean(reader.GetOrdinal("is_reminder")),
                NotificationSent = reader.IsDBNull(reader.GetOrdinal("notification_sent")) ? null : DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("notification_sent")), DateTimeKind.Utc),
                NotificationAddress = reader.IsDBNull(reader.GetOrdinal("notification_address")) ? null : reader.GetString(reader.GetOrdinal("notification_address")),
                NotificationOrderId = reader.IsDBNull(reader.GetOrdinal("notification_order_id")) ? null : reader.GetGuid(reader.GetOrdinal("notification_order_id")),
                ShipmentId = reader.IsDBNull(reader.GetOrdinal("shipment_id")) ? null : reader.GetGuid(reader.GetOrdinal("shipment_id")),
                OrderRequest = reader.IsDBNull(reader.GetOrdinal("order_request")) ? null : reader.GetString(reader.GetOrdinal("order_request"))
            };
        }, cancellationToken);
    }

    public async Task UpdateNotificationSent(Guid notificationId, DateTimeOffset sentTime, string notificationAddress, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.file_transfer_notification " +
            "SET notification_sent = @notificationSent, notification_address = @notificationAddress " +
            "WHERE file_transfer_notification_id_pk = @id");
        command.Parameters.AddWithValue("@id", notificationId);
        command.Parameters.AddWithValue("@notificationSent", sentTime);
        command.Parameters.AddWithValue("@notificationAddress", notificationAddress);

        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }
}
