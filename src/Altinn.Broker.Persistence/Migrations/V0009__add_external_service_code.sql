ALTER TABLE broker.altinn_resource 
ADD external_service_code_legacy varchar(7);
COMMENT ON COLUMN broker.altinn_resource.external_service_code_legacy IS 'Part of legacy solution';

ALTER TABLE broker.altinn_resource 
ADD external_service_edition_code_legacy int;
COMMENT ON COLUMN broker.altinn_resource.external_service_edition_code_legacy IS 'Part of legacy solution';