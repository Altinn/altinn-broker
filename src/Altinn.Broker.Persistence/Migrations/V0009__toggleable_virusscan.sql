ALTER TABLE broker.altinn_resource
ADD approved_for_disabled_virus_scan bool NOT NULL DEFAULT false;

ALTER TABLE broker.storage_provider
ADD active bool NOT NULL DEFAULT true;

ALTER TABLE broker.storage_provider 
ADD CONSTRAINT storage_provider_owner_type_unique 
UNIQUE (service_owner_id_fk, storage_provider_type, active);