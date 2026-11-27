-- Used by the stuckNotification job to find notifications that have been pending for too long and are likely stuck.
CREATE INDEX IF NOT EXISTS file_transfer_notification_pending_status_index
ON broker.file_transfer_notification (file_transfer_id_fk)
WHERE status = 0;