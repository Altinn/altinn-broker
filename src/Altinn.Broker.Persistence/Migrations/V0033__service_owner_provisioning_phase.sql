-- Tracks background storage deployment started from BrokerBox or the service-owner API.
-- Existing rows are already in use, so they stay Ready. New rows are inserted as Queued.
ALTER TABLE broker.service_owner
    ADD COLUMN provisioning_phase character varying(64) NOT NULL DEFAULT 'Ready';
