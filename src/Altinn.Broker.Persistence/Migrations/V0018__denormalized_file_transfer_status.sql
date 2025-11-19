-- 1. Add latest file status columns to file_transfer
ALTER TABLE broker.file_transfer 
ADD COLUMN IF NOT EXISTS latest_file_status_id int4,
ADD COLUMN IF NOT EXISTS latest_file_status_date timestamp;

-- 2. Create NEW table for latest actor statuses per file WITH indexes
CREATE TABLE broker.actor_file_transfer_latest_status (
  file_transfer_id_fk uuid NOT NULL,
  actor_id_fk int8 NOT NULL,
  latest_actor_status_id int4 NOT NULL,
  latest_actor_status_date timestamp NOT NULL,
  
  -- Primary key
  CONSTRAINT actor_file_transfer_latest_status_pkey 
    PRIMARY KEY (file_transfer_id_fk, actor_id_fk),
  
  -- Foreign keys
  CONSTRAINT actor_file_transfer_latest_status_file_fk 
    FOREIGN KEY (file_transfer_id_fk) 
    REFERENCES broker.file_transfer(file_transfer_id_pk) 
    ON DELETE CASCADE,
  CONSTRAINT actor_file_transfer_latest_status_actor_fk 
    FOREIGN KEY (actor_id_fk) 
    REFERENCES broker.actor(actor_id_pk) 
    ON DELETE CASCADE,
  CONSTRAINT actor_file_transfer_latest_status_status_fk 
    FOREIGN KEY (latest_actor_status_id) 
    REFERENCES broker.actor_file_transfer_status_description(actor_file_transfer_status_description_id_pk)
);

-- No need to do concurrently here as it is a new table
CREATE INDEX idx_afls_actor_status 
  ON broker.actor_file_transfer_latest_status (actor_id_fk, latest_actor_status_id, file_transfer_id_fk);

CREATE INDEX idx_afls_file 
  ON broker.actor_file_transfer_latest_status (file_transfer_id_fk);

CREATE INDEX idx_afls_actor_file_status 
  ON broker.actor_file_transfer_latest_status (actor_id_fk, file_transfer_id_fk, latest_actor_status_id);
