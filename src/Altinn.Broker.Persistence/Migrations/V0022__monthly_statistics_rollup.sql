CREATE TABLE broker.monthly_statistics_rollup (
    monthly_statistics_rollup_id_pk bigserial PRIMARY KEY,
    service_owner_id character varying(50) NOT NULL,
    year integer NOT NULL,
    month integer NOT NULL,
    resource_id character varying(100) NOT NULL,
    sender character varying(100) NOT NULL,
    recipient character varying(100) NOT NULL,
    total_file_transfers integer NOT NULL,
    upload_count integer NOT NULL,
    total_transfer_download_attempts integer NOT NULL,
    transfers_with_download_confirmed integer NOT NULL,
    refreshed_at timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_monthly_statistics_rollup_key UNIQUE (service_owner_id, year, month, resource_id, sender, recipient)
);

-- Covers published_in_month CTE: index-only scan on (status, date) returning file_transfer_id
CREATE INDEX idx_file_transfer_status_description_date_transfer
ON broker.file_transfer_status (
    file_transfer_status_description_id_fk,
    file_transfer_status_date,
    file_transfer_id_fk
);

-- Covers actor_activity CTE: date-range scan → GROUP BY (file_transfer_id, actor_id) with status for CASE
CREATE INDEX idx_actor_file_transfer_status_date_pair
ON broker.actor_file_transfer_status (
    actor_file_transfer_status_date,
    file_transfer_id_fk,
    actor_id_fk
)
INCLUDE (actor_file_transfer_status_description_id_fk);