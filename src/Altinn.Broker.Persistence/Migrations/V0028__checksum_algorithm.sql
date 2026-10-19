-- Which digest is stored in checksum. Existing rows stay MD5.
ALTER TABLE broker.file_transfer
    ADD COLUMN checksum_algorithm character varying(16) NOT NULL DEFAULT 'MD5';

ALTER TABLE broker.file_transfer
    ADD CONSTRAINT file_transfer_checksum_algorithm_check
    CHECK (checksum_algorithm IN ('MD5', 'SHA256'));
