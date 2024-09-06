ALTER TABLE broker.altinn_resource
ADD purge_file_transfer_grace_period interval NULL;

ALTER TABLE broker.altinn_resource
ADD purge_file_transfer_after_all_recipients_confirmed bool NOT NULL DEFAULT true;