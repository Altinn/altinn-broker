-- Which digest is stored in checksum. Existing rows stay MD5.
-- NOT VALID records the check without scanning existing rows. Validation is the next migration,
-- in its own transaction, so the ACCESS EXCLUSIVE lock from ADD COLUMN is released first.
ALTER TABLE broker.file_transfer
    ADD COLUMN checksum_algorithm character varying(16) NOT NULL DEFAULT 'MD5';

ALTER TABLE broker.file_transfer
    ADD CONSTRAINT file_transfer_checksum_algorithm_check
    CHECK (checksum_algorithm IN ('MD5', 'SHA256')) NOT VALID;
