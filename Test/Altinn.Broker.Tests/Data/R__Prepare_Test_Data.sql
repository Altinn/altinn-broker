INSERT INTO broker.resource_owner (resource_owner_id_pk, resource_owner_name, file_time_to_live)
VALUES ('0192:991825827', 'Digitaliseringsdirektoratet Avd Oslo', '1 Days');

INSERT INTO broker.storage_provider (storage_provider_id_pk, resource_owner_id_fk, created, storage_provider_type, resource_name)
VALUES (DEFAULT, '0192:991825827', NOW(), 'Altinn3Azure', 'dummy-value');
