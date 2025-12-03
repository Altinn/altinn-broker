ALTER TABLE broker.actor_file_transfer_status
  DROP CONSTRAINT actor_file_transfer_status_file_transfer_id_fk_fkey;

ALTER TABLE broker.actor_file_transfer_status
  ADD CONSTRAINT actor_file_transfer_status_file_transfer_id_fk_fkey
    FOREIGN KEY (file_transfer_id_fk)
    REFERENCES broker.file_transfer(file_transfer_id_pk)
    ON DELETE CASCADE;