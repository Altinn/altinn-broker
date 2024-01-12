INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name, file_time_to_live)
VALUES ('0192:991825827', 'Digitaliseringsdirektoratet Avd Oslo', '1 Days');

INSERT INTO broker.storage_provider (storage_provider_id_pk, service_owner_id_fk, created, storage_provider_type, resource_name)
VALUES (DEFAULT, '0192:991825827', NOW(), 'Altinn3Azure', 'dummy-value');

INSERT INTO broker.service (service_id_pk, created, organization_number, service_owner_id_fk)
VALUES (DEFAULT, NOW(), '0192:991825827', '0192:991825827');

INSERT INTO broker.service (service_id_pk, created, organization_number, service_owner_id_fk)
VALUES (DEFAULT, NOW(), '0192:991825832', '0192:991825827');
