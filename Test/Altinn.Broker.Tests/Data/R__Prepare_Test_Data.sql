INSERT INTO broker.resource_owner (resource_owner_id_pk, resource_owner_name, file_time_to_live)
VALUES ('0192:991825827', 'Digitaliseringsdirektoratet Avd Oslo', '1 Days');

INSERT INTO broker.storage_provider (storage_provider_id_pk, resource_owner_id_fk, created, storage_provider_type, resource_name)
VALUES (DEFAULT, '0192:991825827', NOW(), 'Altinn3Azure', 'dummy-value');

INSERT INTO broker.service (service_id_pk, created, client_id, organization_number, resource_owner_id_fk)
VALUES (DEFAULT, NOW(), '00000000-0000-0000-0000-000000000000', '0192:991825827', '0192:991825827');

INSERT INTO broker.service (service_id_pk, created, client_id, organization_number, resource_owner_id_fk)
VALUES (DEFAULT, NOW(), '11111111-1111-1111-1111-111111111111', '0192:991825832', '0192:991825827');
