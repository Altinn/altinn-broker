-- Scan existing rows now that V0028 has committed and released the ADD COLUMN lock.
-- New rows are already checked; this only marks the constraint valid for rows already stored.
ALTER TABLE broker.file_transfer
    VALIDATE CONSTRAINT file_transfer_checksum_algorithm_check;
