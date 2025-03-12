CREATE INDEX file_transfer_created_idx ON broker.file_transfer (created);
CREATE INDEX resource_created_idx ON broker.altinn_resource (created);
CREATE INDEX file_transfer_sender_actor_id_fk_idx ON broker.file_transfer(sender_actor_id_fk)