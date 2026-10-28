using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
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
                custom_recipient_type,
                custom_recipient_identifier,
                custom_recipient_related_organization,
                notification_template,
                notification_channel,
                requested_send_time,
                created,
                is_reminder,
                notification_order_id,
                shipment_id,
                status,
                status_reason,
                order_request
            )
            VALUES (
                @id,
                @fileTransferId,
                @actorId,
                @customRecipientType,
                @customRecipientIdentifier,
                @customRecipientRelatedOrganization,
                @notificationTemplate,
                @notificationChannel,
                @requestedSendTime,
                @created,
                @isReminder,
                @notificationOrderId,
                @shipmentId,
                @status,
                @statusReason,
                @orderRequest
            );";

        await using var command = dataSource.CreateCommand(query);
        command.Parameters.AddWithValue("@id", notification.Id);
        command.Parameters.AddWithValue("@fileTransferId", notification.FileTransferId);
        command.Parameters.AddWithValue("@actorId", (object?)notification.ActorId ?? DBNull.Value);
        command.Parameters.AddWithValue("@customRecipientType", notification.CustomRecipientType is null ? DBNull.Value : (short)notification.CustomRecipientType.Value);
        command.Parameters.AddWithValue("@customRecipientIdentifier", (object?)notification.CustomRecipientIdentifier ?? DBNull.Value);
        command.Parameters.AddWithValue("@customRecipientRelatedOrganization", (object?)notification.CustomRecipientRelatedOrganization ?? DBNull.Value);
        command.Parameters.AddWithValue("@notificationTemplate", (int)notification.NotificationTemplate);
        command.Parameters.AddWithValue("@notificationChannel", (int)notification.NotificationChannel);
        command.Parameters.AddWithValue("@requestedSendTime", notification.RequestedSendTime);
        command.Parameters.AddWithValue("@created", notification.Created);
        command.Parameters.AddWithValue("@isReminder", notification.IsReminder);
        command.Parameters.AddWithValue("@notificationOrderId", (object?)notification.NotificationOrderId ?? DBNull.Value);
        command.Parameters.AddWithValue("@shipmentId", (object?)notification.ShipmentId ?? DBNull.Value);
        command.Parameters.AddWithValue("@status", (short)notification.Status);
        command.Parameters.AddWithValue("@statusReason", (object?)notification.StatusReason ?? DBNull.Value);
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
                    CustomRecipientType = reader.IsDBNull(reader.GetOrdinal("custom_recipient_type")) ? null : (CustomRecipientType)reader.GetInt16(reader.GetOrdinal("custom_recipient_type")),
                    CustomRecipientIdentifier = reader.IsDBNull(reader.GetOrdinal("custom_recipient_identifier")) ? null : reader.GetString(reader.GetOrdinal("custom_recipient_identifier")),
                    CustomRecipientRelatedOrganization = reader.IsDBNull(reader.GetOrdinal("custom_recipient_related_organization")) ? null : reader.GetString(reader.GetOrdinal("custom_recipient_related_organization")),
                    NotificationTemplate = (NotificationTemplate)reader.GetInt32(reader.GetOrdinal("notification_template")),
                    NotificationChannel = (NotificationChannel)reader.GetInt32(reader.GetOrdinal("notification_channel")),
                    RequestedSendTime = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("requested_send_time")), DateTimeKind.Utc),
                    Created = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("created")), DateTimeKind.Utc),
                    IsReminder = reader.GetBoolean(reader.GetOrdinal("is_reminder")),
                    NotificationSent = reader.IsDBNull(reader.GetOrdinal("notification_sent")) ? null : DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("notification_sent")), DateTimeKind.Utc),
                    NotificationAddress = reader.IsDBNull(reader.GetOrdinal("notification_address")) ? null : reader.GetString(reader.GetOrdinal("notification_address")),
                    NotificationOrderId = reader.IsDBNull(reader.GetOrdinal("notification_order_id")) ? null : reader.GetGuid(reader.GetOrdinal("notification_order_id")),
                    ShipmentId = reader.IsDBNull(reader.GetOrdinal("shipment_id")) ? null : reader.GetGuid(reader.GetOrdinal("shipment_id")),
                    Status = (NotificationDeliveryStatus)reader.GetInt16(reader.GetOrdinal("status")),
                    StatusReason = reader.IsDBNull(reader.GetOrdinal("status_reason")) ? null : reader.GetString(reader.GetOrdinal("status_reason")),
                    OrderRequest = reader.IsDBNull(reader.GetOrdinal("order_request")) ? null : reader.GetString(reader.GetOrdinal("order_request"))
                });
            }
            return notifications;
        }, cancellationToken);
    }

    public async Task UpdateOrderResponseData(Guid notificationId, Guid notificationOrderId, Guid shipmentId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.file_transfer_notification " +
            "SET notification_order_id = @notificationOrderId, shipment_id = @shipmentId, status = @status " +
            "WHERE file_transfer_notification_id_pk = @id");
        command.Parameters.AddWithValue("@id", notificationId);
        command.Parameters.AddWithValue("@notificationOrderId", notificationOrderId);
        command.Parameters.AddWithValue("@shipmentId", shipmentId);
        command.Parameters.AddWithValue("@status", (short)NotificationDeliveryStatus.Ordered);

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
                CustomRecipientType = reader.IsDBNull(reader.GetOrdinal("custom_recipient_type")) ? null : (CustomRecipientType)reader.GetInt16(reader.GetOrdinal("custom_recipient_type")),
                CustomRecipientIdentifier = reader.IsDBNull(reader.GetOrdinal("custom_recipient_identifier")) ? null : reader.GetString(reader.GetOrdinal("custom_recipient_identifier")),
                CustomRecipientRelatedOrganization = reader.IsDBNull(reader.GetOrdinal("custom_recipient_related_organization")) ? null : reader.GetString(reader.GetOrdinal("custom_recipient_related_organization")),
                NotificationTemplate = (NotificationTemplate)reader.GetInt32(reader.GetOrdinal("notification_template")),
                NotificationChannel = (NotificationChannel)reader.GetInt32(reader.GetOrdinal("notification_channel")),
                RequestedSendTime = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("requested_send_time")), DateTimeKind.Utc),
                Created = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("created")), DateTimeKind.Utc),
                IsReminder = reader.GetBoolean(reader.GetOrdinal("is_reminder")),
                NotificationSent = reader.IsDBNull(reader.GetOrdinal("notification_sent")) ? null : DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("notification_sent")), DateTimeKind.Utc),
                NotificationAddress = reader.IsDBNull(reader.GetOrdinal("notification_address")) ? null : reader.GetString(reader.GetOrdinal("notification_address")),
                NotificationOrderId = reader.IsDBNull(reader.GetOrdinal("notification_order_id")) ? null : reader.GetGuid(reader.GetOrdinal("notification_order_id")),
                ShipmentId = reader.IsDBNull(reader.GetOrdinal("shipment_id")) ? null : reader.GetGuid(reader.GetOrdinal("shipment_id")),
                Status = (NotificationDeliveryStatus)reader.GetInt16(reader.GetOrdinal("status")),
                StatusReason = reader.IsDBNull(reader.GetOrdinal("status_reason")) ? null : reader.GetString(reader.GetOrdinal("status_reason")),
                OrderRequest = reader.IsDBNull(reader.GetOrdinal("order_request")) ? null : reader.GetString(reader.GetOrdinal("order_request"))
            };
        }, cancellationToken);
    }

    public async Task UpdateNotificationSent(Guid notificationId, DateTimeOffset sentTime, string notificationAddress, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.file_transfer_notification " +
            "SET notification_sent = @notificationSent, notification_address = @notificationAddress, status = @status " +
            "WHERE file_transfer_notification_id_pk = @id");
        command.Parameters.AddWithValue("@id", notificationId);
        command.Parameters.AddWithValue("@notificationSent", sentTime);
        command.Parameters.AddWithValue("@notificationAddress", notificationAddress);
        command.Parameters.AddWithValue("@status", (short)NotificationDeliveryStatus.Sent);

        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }

    public async Task UpdateStatus(Guid notificationId, NotificationDeliveryStatus status, string? statusReason, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.file_transfer_notification " +
            "SET status = @status, status_reason = @statusReason " +
            "WHERE file_transfer_notification_id_pk = @id");
        command.Parameters.AddWithValue("@id", notificationId);
        command.Parameters.AddWithValue("@status", (short)status);
        command.Parameters.AddWithValue("@statusReason", (object?)statusReason ?? DBNull.Value);

        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }

    public async Task<List<FileTransferWithPendingNotifications>> GetFileTransfersWithPendingNotifications(DateTimeOffset publishedBefore, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(@"
            SELECT n.file_transfer_id_fk,
                   f.latest_file_status_id,
                   MIN(s.file_transfer_status_date) AS published_at
            FROM broker.file_transfer_notification n
            JOIN broker.file_transfer f ON f.file_transfer_id_pk = n.file_transfer_id_fk
            JOIN broker.file_transfer_status s
                ON s.file_transfer_id_fk = n.file_transfer_id_fk
                AND s.file_transfer_status_description_id_fk = @publishedStatus
            WHERE n.status = @pendingStatus
            GROUP BY n.file_transfer_id_fk, f.latest_file_status_id
            HAVING MIN(s.file_transfer_status_date) < @publishedBefore");
        command.Parameters.AddWithValue("@publishedStatus", (int)FileTransferStatus.Published);
        command.Parameters.AddWithValue("@pendingStatus", (short)NotificationDeliveryStatus.Pending);
        command.Parameters.AddWithValue("@publishedBefore", publishedBefore.UtcDateTime);

        return await commandExecutor.ExecuteWithRetry(async (ct) =>
        {
            var fileTransfers = new List<FileTransferWithPendingNotifications>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                fileTransfers.Add(new FileTransferWithPendingNotifications(
                    reader.GetGuid(reader.GetOrdinal("file_transfer_id_fk")),
                    DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("published_at")), DateTimeKind.Utc),
                    reader.IsDBNull(reader.GetOrdinal("latest_file_status_id")) ? null : (FileTransferStatus)reader.GetInt32(reader.GetOrdinal("latest_file_status_id"))));
            }
            return fileTransfers;
        }, cancellationToken);
    }

    public async Task UpdatePendingStatusesForFileTransfer(Guid fileTransferId, NotificationDeliveryStatus status, string? statusReason, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE broker.file_transfer_notification " +
            "SET status = @status, status_reason = @statusReason " +
            "WHERE file_transfer_id_fk = @fileTransferId AND status = @pendingStatus");
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@status", (short)status);
        command.Parameters.AddWithValue("@statusReason", (object?)statusReason ?? DBNull.Value);
        command.Parameters.AddWithValue("@pendingStatus", (short)NotificationDeliveryStatus.Pending);

        await commandExecutor.ExecuteWithRetry(command.ExecuteNonQueryAsync, cancellationToken);
    }
}
