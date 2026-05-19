-- Track the vendor that authenticated to Maskinporten.
-- For system-user flows this is the org that owns the registered system; for direct flows it is the
-- caller themselves. Captured per status transition that is caused by an actor's API call:
--   * file_transfer_status: sender-side actions (Initialized, UploadStarted, Cancelled).
--     System-caused transitions (UploadProcessing, Published, AllConfirmedDownloaded, Purged, Failed) leave vendor NULL.
--   * actor_file_transfer_status: recipient-side actions (DownloadStarted, DownloadConfirmed).

ALTER TABLE broker.file_transfer_status
    ADD COLUMN vendor character varying(100) NULL;

ALTER TABLE broker.actor_file_transfer_status
    ADD COLUMN vendor character varying(100) NULL;

ALTER TABLE broker.monthly_statistics_rollup
    ADD COLUMN sender_vendor character varying(100) NOT NULL DEFAULT '',
    ADD COLUMN recipient_vendor character varying(100) NOT NULL DEFAULT '';

ALTER TABLE broker.monthly_statistics_rollup
    DROP CONSTRAINT uq_monthly_statistics_rollup_key;

ALTER TABLE broker.monthly_statistics_rollup
    ADD CONSTRAINT uq_monthly_statistics_rollup_key UNIQUE (
        service_owner_id, year, month, resource_id, sender, recipient, sender_vendor, recipient_vendor
    );

-- Supports the per-month DELETE in RebuildMonthlyStatisticsRollupForMonth, which filters on (year, month)
-- without a service_owner_id prefix and therefore can't use the unique key as a range index.
CREATE INDEX idx_monthly_statistics_rollup_year_month
ON broker.monthly_statistics_rollup (year, month);

-- Sender vendor lookup in the monthly rollup: per-transfer seek for the Initialized row's vendor.
-- Partial index keeps it small (one Initialized row per transfer) and enables index-only scans.
CREATE INDEX idx_file_transfer_status_initialized_vendor
ON broker.file_transfer_status (file_transfer_id_fk)
INCLUDE (vendor, file_transfer_status_date, file_transfer_status_id_pk)
WHERE file_transfer_status_description_id_fk = 0;

-- Recipient vendor lookup in the monthly rollup: latest-vendor lookup per (transfer, actor).
-- Partial index on rows where vendor is populated; DESC ordering matches the ORDER BY in the lateral.
CREATE INDEX idx_actor_file_transfer_status_vendor
ON broker.actor_file_transfer_status (
    file_transfer_id_fk,
    actor_id_fk,
    actor_file_transfer_status_date DESC,
    actor_file_transfer_status_id_pk DESC
)
INCLUDE (vendor)
WHERE vendor IS NOT NULL;
