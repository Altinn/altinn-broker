INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name, file_time_to_live)
VALUES ('0192:991825827', 'Digitaliseringsdirektoratet Avd Oslo', '1 Days');

INSERT INTO broker.storage_provider (storage_provider_id_pk, service_owner_id_fk, created, storage_provider_type, resource_name)
VALUES (DEFAULT, '0192:991825827', NOW(), 'Altinn3Azure', 'dummy-value');

INSERT INTO broker.file (file_id_pk, service_owner_id_fk, filename, checksum, sender_actor_id_fk, external_file_reference, created, storage_provider_id_fk, file_location, expiration_time)
VALUES ('ed76ce89-3768-481a-bca1-4e4262d9098b', '0192:991825827', 'filename.txt', NULL, 1, 'external_reference', NOW(), 1, 'https://blob-storage-example', NOW() + INTERVAL '1 Days');
