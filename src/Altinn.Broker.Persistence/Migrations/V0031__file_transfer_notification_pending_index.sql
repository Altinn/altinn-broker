-- For StuckNotificationOrderHandler, which periodically looks for notifications still Pending (0) after publish.
-- Built concurrently so writes to the table are not blocked meanwhile. CONCURRENTLY cannot run inside a transaction,
-- so this is the only statement in the migration.
CREATE INDEX CONCURRENTLY IF NOT EXISTS file_transfer_notification_pending_idx
    ON broker.file_transfer_notification (file_transfer_id_fk)
    WHERE status = 0;
