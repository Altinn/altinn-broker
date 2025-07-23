CREATE INDEX actor_file_transfer_status_file_transfer_id_fk_actor_id_fk_idx 
ON broker.actor_file_transfer_status (
    file_transfer_id_fk,
    actor_id_fk,
    actor_file_transfer_status_description_id_fk DESC
);

CREATE INDEX file_transfer_status_file_transfer_status_id_pk_file_transfer_id_fk_idx 
ON broker.file_transfer_status (
    file_transfer_id_fk,
    file_transfer_status_id_pk DESC
);

CREATE INDEX file_transfer_file_transfer_id_pk_resource_id_idx 
ON broker.file_transfer (
    file_transfer_id_pk,
    resource_id
);
