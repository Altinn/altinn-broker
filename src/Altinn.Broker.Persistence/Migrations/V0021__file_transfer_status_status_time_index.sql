CREATE INDEX IF NOT EXISTS idx_file_transfer_status_lookup
ON broker.file_transfer_status (
    file_transfer_id_fk,
    file_transfer_status_description_id_fk,
    file_transfer_status_date DESC
);