-- Where each notification is on its way to the recipient (NotificationDeliveryStatus), so a notification Altinn
-- Notifications refused (422) or could not deliver can be told apart from one still on its way.
ALTER TABLE broker.file_transfer_notification
    ADD COLUMN status smallint NOT NULL DEFAULT 0,
    -- Why it was rejected or not delivered, as Altinn Notifications explained it.
    ADD COLUMN status_reason character varying(1000) NULL;

-- Rows from before this migration: Sent (3) when delivery was confirmed, Ordered (1) when Altinn Notifications
-- accepted the order, otherwise left as Pending (0). A rejected order cannot be told apart from a pending one
-- after the fact, so those stay Pending.
UPDATE broker.file_transfer_notification SET status = 3 WHERE notification_sent IS NOT NULL;
UPDATE broker.file_transfer_notification SET status = 1 WHERE notification_sent IS NULL AND shipment_id IS NOT NULL;
