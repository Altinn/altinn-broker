INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name)
VALUES ('0192:991825827', 'Digitaliseringsdirektoratet Avd Oslo');

INSERT INTO broker.storage_provider (storage_provider_id_pk, service_owner_id_fk, created, storage_provider_type, resource_name)
VALUES (DEFAULT, '0192:991825827', NOW(), 'Altinn3Azure', 'dummy-value');
