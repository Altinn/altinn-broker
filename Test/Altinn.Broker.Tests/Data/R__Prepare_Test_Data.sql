INSERT INTO broker.resource_owner (resource_owner_id_pk, resource_owner_name, file_time_to_live, resource_group_name)
VALUES ('0192:991825827', 'Digitaliseringsdirektoratet Avd Oslo', '1 Days', '00010000-0000-0000-0000-00a000000000');

INSERT INTO broker.storage_provider (storage_provider_id_pk, resource_owner_id_fk, created, storage_provider_type, resource_name)
VALUES (DEFAULT, '0192:991825827', NOW(), 'Altinn3Azure', 'dummy-value');
