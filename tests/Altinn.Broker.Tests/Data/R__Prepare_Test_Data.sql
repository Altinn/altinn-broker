INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name)
VALUES ('0192:991825827', 'Digitaliseringsdirektoratet Avd Oslo')
ON CONFLICT (service_owner_id_pk) DO NOTHING;

INSERT INTO broker.storage_provider (storage_provider_id_pk, service_owner_id_fk, created, storage_provider_type, resource_name, active)
VALUES (DEFAULT, '0192:991825827', NOW(), 'Altinn3Azure', 'dummy-value', true)
ON CONFLICT (service_owner_id_fk, storage_provider_type, active) DO NOTHING;

INSERT INTO broker.altinn_resource (
    resource_id_pk,
    created,
    max_file_transfer_size,
    file_transfer_time_to_live,
    organization_number,
    service_owner_id_fk
) VALUES (
    'altinn-broker-test-resource-1',
    NOW(),
    null,
    null,
    '991825827',
    '0192:991825827'
)
ON CONFLICT (resource_id_pk) DO NOTHING;

INSERT INTO broker.altinn_resource (
    resource_id_pk,
    created,
    max_file_transfer_size,
    file_transfer_time_to_live,
    organization_number,
    service_owner_id_fk,
    use_manifest_file_shim
) VALUES (
    'manifest-shim-resource',
    NOW(),
    null,
    null,
    '991825827',
    '0192:991825827',
    true
)
ON CONFLICT (resource_id_pk) DO NOTHING;

INSERT INTO broker.altinn_resource (
    resource_id_pk,
    created,
    max_file_transfer_size,
    file_transfer_time_to_live,
    organization_number,
    service_owner_id_fk
) VALUES (
    'ttd-brokertestressurs',
    NOW(),
    null,
    null,
    '991825827',
    '0192:991825827'
)
ON CONFLICT (resource_id_pk) DO NOTHING;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'broker'
          AND table_name = 'altinn_resource'
          AND column_name = 'required_party'
          AND data_type = 'boolean'
    ) THEN
        UPDATE broker.altinn_resource
        SET required_party = true
        WHERE resource_id_pk = 'ttd-brokertestressurs';
    ELSE
        UPDATE broker.altinn_resource
        SET required_party = '991825827'
        WHERE resource_id_pk = 'ttd-brokertestressurs';
    END IF;
END $$;

INSERT INTO broker.altinn_resource (
    resource_id_pk,
    created,
    max_file_transfer_size,
    file_transfer_time_to_live,
    organization_number,
    service_owner_id_fk
) VALUES (
    'ttd-broker-tilgangsliste',
    NOW(),
    null,
    null,
    '991825827',
    '0192:991825827'
)
ON CONFLICT (resource_id_pk) DO NOTHING;