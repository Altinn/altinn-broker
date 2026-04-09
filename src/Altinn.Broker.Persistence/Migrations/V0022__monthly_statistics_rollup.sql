CREATE TABLE broker.monthly_statistics_monthly_rollup (
    monthly_statistics_monthly_rollup_id_pk bigserial PRIMARY KEY,
    service_owner_id character varying(50) NOT NULL,
    year integer NOT NULL,
    month integer NOT NULL,
    resource_id character varying(100) NOT NULL,
    sender character varying(100) NOT NULL,
    recipient character varying(100) NOT NULL,
    groupable_property_values jsonb NOT NULL DEFAULT '{}'::jsonb,
    total_file_transfers integer NOT NULL,
    upload_count integer NOT NULL,
    download_attempt_count integer NOT NULL,
    unique_download_started_count integer NOT NULL,
    download_confirmed_count integer NOT NULL,
    refreshed_at timestamp without time zone NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_monthly_statistics_rollup_service_owner_month_resource
ON broker.monthly_statistics_monthly_rollup (
    service_owner_id,
    year,
    month,
    resource_id
);

CREATE INDEX idx_monthly_statistics_rollup_service_owner_month_participants
ON broker.monthly_statistics_monthly_rollup (
    service_owner_id,
    year,
    month,
    resource_id,
    sender,
    recipient
);
