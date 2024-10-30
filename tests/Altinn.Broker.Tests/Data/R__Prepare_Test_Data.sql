INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name)
VALUES ('0192:991825827', 'Digitaliseringsdirektoratet Avd Oslo');

INSERT INTO broker.storage_provider (storage_provider_id_pk, service_owner_id_fk, created, storage_provider_type, resource_name)
VALUES (DEFAULT, '0192:991825827', NOW(), 'Azurite', 'dummy-value');

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
);

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
);
