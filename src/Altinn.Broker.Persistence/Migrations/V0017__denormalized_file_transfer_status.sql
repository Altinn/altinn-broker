-- 1. Add latest file status columns to file_transfer (as before)
ALTER TABLE broker.file_transfer 
ADD COLUMN IF NOT EXISTS latest_file_status_id int4,
ADD COLUMN IF NOT EXISTS latest_file_status_date timestamp;

-- 2. Create NEW table for latest actor statuses per file
CREATE TABLE broker.actor_file_transfer_latest_status (
  file_transfer_id_fk uuid NOT NULL,
  actor_id_fk int8 NOT NULL,
  latest_actor_status_id int4 NOT NULL,
  latest_actor_status_date timestamp NOT NULL,
  CONSTRAINT actor_file_transfer_latest_status_pkey 
    PRIMARY KEY (file_transfer_id_fk, actor_id_fk),
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

-- Indexes for the new lookup table
CREATE INDEX CONCURRENTLY idx_afls_actor_status 
ON broker.actor_file_transfer_latest_status (actor_id_fk, latest_actor_status_id, file_transfer_id_fk);

CREATE INDEX CONCURRENTLY idx_afls_file 
ON broker.actor_file_transfer_latest_status (file_transfer_id_fk);

CREATE INDEX CONCURRENTLY idx_afls_actor_file_status 
ON broker.actor_file_transfer_latest_status (actor_id_fk, file_transfer_id_fk, latest_actor_status_id);

-- Index on file_transfer for combined queries
CREATE INDEX CONCURRENTLY idx_file_transfer_latest_status_resource 
ON broker.file_transfer (latest_file_status_id, resource_id, created)
WHERE latest_file_status_id IS NOT NULL;

-- Trigger for file_transfer_status
CREATE OR REPLACE FUNCTION broker.update_latest_file_status()
RETURNS TRIGGER AS $$
BEGIN
  UPDATE broker.file_transfer
  SET 
    latest_file_status_id = NEW.file_transfer_status_description_id_fk,
    latest_file_status_date = NEW.file_transfer_status_date
  WHERE file_transfer_id_pk = NEW.file_transfer_id_fk
    AND (
      latest_file_status_date IS NULL 
      OR NEW.file_transfer_status_date > latest_file_status_date
      OR (NEW.file_transfer_status_date = latest_file_status_date 
          AND NEW.file_transfer_status_id_pk > COALESCE(
            (SELECT file_transfer_status_id_pk 
             FROM broker.file_transfer_status 
             WHERE file_transfer_id_fk = NEW.file_transfer_id_fk 
               AND file_transfer_status_date = NEW.file_transfer_status_date
             ORDER BY file_transfer_status_id_pk DESC 
             LIMIT 1), 0)
      )
    );
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_update_latest_file_status
AFTER INSERT ON broker.file_transfer_status
FOR EACH ROW
EXECUTE FUNCTION broker.update_latest_file_status();

-- Trigger for actor_file_transfer_status
CREATE OR REPLACE FUNCTION broker.update_latest_actor_status()
RETURNS TRIGGER AS $$
BEGIN
  INSERT INTO broker.actor_file_transfer_latest_status (
    file_transfer_id_fk,
    actor_id_fk,
    latest_actor_status_id,
    latest_actor_status_date
  )
  VALUES (
    NEW.file_transfer_id_fk,
    NEW.actor_id_fk,
    NEW.actor_file_transfer_status_description_id_fk,
    NEW.actor_file_transfer_status_date
  )
  ON CONFLICT (file_transfer_id_fk, actor_id_fk) 
  DO UPDATE SET
    latest_actor_status_id = NEW.actor_file_transfer_status_description_id_fk,
    latest_actor_status_date = NEW.actor_file_transfer_status_date
  WHERE 
    NEW.actor_file_transfer_status_date > actor_file_transfer_latest_status.latest_actor_status_date
    OR (NEW.actor_file_transfer_status_date = actor_file_transfer_latest_status.latest_actor_status_date
        AND NEW.actor_file_transfer_status_id_pk > COALESCE(
          (SELECT actor_file_transfer_status_id_pk
           FROM broker.actor_file_transfer_status
           WHERE file_transfer_id_fk = NEW.file_transfer_id_fk
             AND actor_id_fk = NEW.actor_id_fk
             AND actor_file_transfer_status_date = NEW.actor_file_transfer_status_date
           ORDER BY actor_file_transfer_status_id_pk DESC
           LIMIT 1), 0)
    );
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_update_latest_actor_status
AFTER INSERT ON broker.actor_file_transfer_status
FOR EACH ROW
EXECUTE FUNCTION broker.update_latest_actor_status();

-- Note: Backfilling of existing data will be done in a separate migration to avoid long locks here.
-- but the backfill needs this index
CREATE INDEX CONCURRENTLY idx_file_transfer_null_status 
ON broker.file_transfer (file_transfer_id_pk)
WHERE latest_file_status_id IS NULL;