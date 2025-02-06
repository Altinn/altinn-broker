CREATE INDEX file_transfer_resource_id_idx ON broker.file_transfer (resource_id);
CREATE INDEX actor_actor_external_id_idx ON broker.actor (actor_external_id);
CREATE INDEX file_transfer_status_file_transfer_id_fk_idx ON broker.file_transfer_status (file_transfer_id_fk);
CREATE INDEX actor_file_transfer_status_file_transfer_id_fk_idx ON broker.actor_file_transfer_status (file_transfer_id_fk);
CREATE INDEX actor_file_transfer_status_actor_id_fk_idx ON broker.actor_file_transfer_status (actor_id_fk);
CREATE INDEX actor_file_transfer_status_description_idx ON broker.actor_file_transfer_status (file_transfer_id_fk, actor_file_transfer_status_description_id_fk);
CREATE INDEX file_transfer_status_status_description_id_fk_idx ON broker.file_transfer_status (file_transfer_status_description_id_fk, file_transfer_id_fk);
