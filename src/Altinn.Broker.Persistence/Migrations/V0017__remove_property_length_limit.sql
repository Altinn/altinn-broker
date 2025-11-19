-- Remove DB-level length limits for file transfer properties.

ALTER TABLE broker.file_transfer_property
    ALTER COLUMN key TYPE character varying;

ALTER TABLE broker.file_transfer_property
    ALTER COLUMN value TYPE character varying;
